// ==========================================================================
// AgvOrchestratorTickTests.cs — Layer 3: TickAsync simulation tests
// ==========================================================================
// Simulates the AGV control loop by calling TickAsync() manually.
// No Task.Delay, no real timers — runs in microseconds, zero flakiness.
//
// Pattern: "Game Loop / Robotic Loop Testing"
//   1. Setup mock state (Modbus returns fixed AgvState)
//   2. Call StartTrip() to inject target
//   3. Loop TickAsync() N times — each tick = 100ms of real time
//   4. Assert final state / motor commands
//
// TickAsync must be marked `internal` in AgvOrchestrator.cs
// (InternalsVisibleTo("AgvControl.Tests") already set in AssemblyInfo.cs)
//
// Scenarios covered:
//   1. Happy path: Spinning → Moving → Arrive → Idle
//   2. No jitter: aligned tick sends Stop-spin, NOT Move
//   3. Obstacle block → replan → Spinning
//   4. A* fail → STOP + keep target + Spinning
//   5. Replan cooldown: two back-to-back blocks → only 1 replan
//   6. EmergencyStop mid-trip → motors cut immediately
// ==========================================================================

using System.Drawing;
using AgvControl.Data;
using AgvControl.Models;
using AgvControl.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AgvControl.Tests;

public class AgvOrchestratorTickTests
{
    // -----------------------------------------------------------------------
    // Constants mirroring AgvOrchestrator internals
    // -----------------------------------------------------------------------
    private const int    MoveRpm             = 300;
    private const int    SpinRpm             = 200;
    private const double HeadingThresholdDeg = 10.0;
    private const double WaypointReachedMm   = 250.0;
    private const int    CellSizeMm          = 500;

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// AGV at center of grid cell (2,2) = (1250, 1250) mm, heading 0°.
    /// Safe interior position — away from perimeter walls and shelves.
    /// </summary>
    private static AgvState AgvAtCell(int gx, int gy, double headingDeg = 0) => new()
    {
        PositionX      = gx * CellSizeMm + CellSizeMm / 2,
        PositionY      = gy * CellSizeMm + CellSizeMm / 2,
        HeadingDegrees = headingDeg,
        BatteryLevel   = 100,
        Status         = StatusCode.Idle,
        Error          = ErrorCode.Ok,
    };

    /// <summary>
    /// AGV positioned exactly at waypoint center — triggers "arrived" on next tick.
    /// </summary>
    private static AgvState AgvArrivedAt(Point waypoint) =>
        AgvAtCell(waypoint.X, waypoint.Y);

    /// <summary>Build orchestrator with full mock control.</summary>
    private static (AgvOrchestrator orch, IModbusClient modbus,
                    IVisionClient vision, IPathPlanner planner, IDbLogger db)
        Build(AgvState? initialState = null)
    {
        var modbus  = Substitute.For<IModbusClient>();
        var vision  = Substitute.For<IVisionClient>();
        var planner = Substitute.For<IPathPlanner>();
        var db      = Substitute.For<IDbLogger>();

        modbus.ReadStatusAsync().Returns(initialState ?? AgvAtCell(2, 2));
        vision.GetLatestDetectionsAsync().Returns((VisionResponse?)null);

        var orch = new AgvOrchestrator(
            vision, modbus, planner, db,
            NullLogger<AgvOrchestrator>.Instance);

        return (orch, modbus, vision, planner, db);
    }

    private static CancellationToken NoCancellation => CancellationToken.None;

    // =======================================================================
    // Scenario 1 — Happy path: Spinning → Moving → Arrive → Idle
    // =======================================================================

    [Fact]
    public async Task HappyPath_SpinningToMovingToIdle_CompletesTrip()
    {
        // ── Setup ────────────────────────────────────────────────────────────
        // Single waypoint: grid (5,2) — due East from AGV at (2,2)
        // AGV heading = 0° (East) → already aligned → no spin needed

        var waypoint  = new Point(5, 2);
        var agvState  = AgvAtCell(2, 2, headingDeg: 0);

        var (orch, modbus, _, planner, db) = Build(agvState);

        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns([waypoint]);

        // ── Tick 1: StartTrip + first Spinning tick ──────────────────────────
        orch.StartTrip(
            waypoint.X * CellSizeMm + CellSizeMm / 2.0,  // 2750
            waypoint.Y * CellSizeMm + CellSizeMm / 2.0); // 1250

        await orch.TickAsync(NoCancellation); // Spinning → aligned → transition to Moving

        // ── Tick 2: Moving, not arrived yet ──────────────────────────────────
        await orch.TickAsync(NoCancellation); // sends MOVE 300/300

        await modbus.Received().WriteMotorCommandAsync(MoveRpm, MoveRpm, CommandCode.Move);

        // ── Tick 3: AGV arrives at waypoint ──────────────────────────────────
        // Simulate AGV moved to waypoint center
        modbus.ReadStatusAsync().Returns(AgvArrivedAt(waypoint));

        await orch.TickAsync(NoCancellation); // distance < 250mm → arrived → Idle

        // ── Assert: STOP sent, trip completed, DB logged ─────────────────────
        await modbus.Received().WriteMotorCommandAsync(0, 0, CommandCode.Stop);
        await db.Received().LogPathAsync(
            Arg.Is<PathRecord>(r => r.Status == "completed"));
    }

    // =======================================================================
    // Scenario 2 — No jitter: aligned tick sends Stop-spin, NOT Move
    // =======================================================================

    [Fact]
    public async Task NoJitter_AlignedTick_ShouldNotSendMoveInSameTick()
    {
        // AGV heading 0° (East), waypoint due East → already aligned
        var waypoint = new Point(5, 2);
        var agvState = AgvAtCell(2, 2, headingDeg: 0);

        var (orch, modbus, _, planner, _) = Build(agvState);

        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns([waypoint]);

        orch.StartTrip(2750, 1250);

        // Single tick — Spinning, heading is aligned
        await orch.TickAsync(NoCancellation);

        // Must send Stop (stop spinning), must NOT send Move in this same tick
        await modbus.Received().WriteMotorCommandAsync(0, 0, CommandCode.Stop);
        await modbus.DidNotReceive().WriteMotorCommandAsync(MoveRpm, MoveRpm, CommandCode.Move);
    }

    // =======================================================================
    // Scenario 3 — Obstacle block → replan → Spinning
    // =======================================================================

    [Fact]
    public async Task ObstacleBlock_ShouldReplanAndTransitionToSpinning()
    {
        // AGV at (2,2), waypoint at (5,2)
        // After first move tick, obstacle appears ON waypoint[0]
        var waypoint    = new Point(5, 2);
        var agvState    = AgvAtCell(2, 2, headingDeg: 0);
        var newWaypoint = new Point(5, 3); // replan detour

        var (orch, modbus, vision, planner, _) = Build(agvState);

        // Initial plan: one waypoint straight East
        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns([waypoint]);

        orch.StartTrip(2750, 1250);

        // Tick 1: Spinning → aligned → Moving
        await orch.TickAsync(NoCancellation);

        // Tick 2: Moving — inject obstacle ON waypoint cell
        vision.GetLatestDetectionsAsync().Returns(new VisionResponse
        {
            Detections =
            [
                new Detection
                {
                    ObjectClass    = "box",
                    Confidence     = 0.95,
                    // Distance such that obstacle maps to cell (5,2)
                    // AGV at x=1250, heading 0° → obstacle at 1250 + dist + 300 = 5*500+250 = 2750
                    // dist_mm = 2750 - 1250 - 300 = 1200mm = 1.2m
                    DistanceMeters = 1.2,
                }
            ],
            TotalObjects = 1,
        });

        // Replan returns detour
        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns([waypoint, newWaypoint], [new List<Point> { newWaypoint }]);

        await orch.TickAsync(NoCancellation);

        // Replan was triggered — FindPath called at least twice (initial + replan)
        planner.Received(2).FindPath(
            Arg.Any<GridMap>(),
            Arg.Any<Point>(),
            Arg.Any<Point>());

        // After replan, state should be Spinning (re-align to new waypoint)
        // Verified indirectly: next tick should NOT send MOVE without aligning first
        modbus.ClearReceivedCalls();
        await orch.TickAsync(NoCancellation);
        await modbus.DidNotReceive().WriteMotorCommandAsync(MoveRpm, MoveRpm, CommandCode.Move);
    }

    // =======================================================================
    // Scenario 4 — A* fail → STOP + keep target + Spinning
    // =======================================================================

    [Fact]
    public async Task AStarFail_ShouldStopAndKeepTargetForRetry()
    {
        var waypoint = new Point(5, 2);
        var agvState = AgvAtCell(2, 2, headingDeg: 0);

        var (orch, modbus, vision, planner, db) = Build(agvState);

        // Initial plan succeeds
        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns([waypoint]);

        orch.StartTrip(2750, 1250);

        // Tick 1: Spinning → aligned → Moving
        await orch.TickAsync(NoCancellation);

        // Tick 2: Moving — inject obstacle to trigger replan
        vision.GetLatestDetectionsAsync().Returns(new VisionResponse
        {
            Detections = [new Detection { ObjectClass = "box", Confidence = 0.9, DistanceMeters = 1.2 }],
            TotalObjects = 1,
        });

        // Replan throws — no path
        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Throws(new InvalidOperationException("no path found — completely blocked"));

        await orch.TickAsync(NoCancellation);

        // STOP must have been sent
        await modbus.Received().WriteMotorCommandAsync(0, 0, CommandCode.Stop);

        // Error must have been logged
        await db.Received().LogSystemEventAsync(
            level:             Arg.Is<string>(s => s == "error"),
            component:         Arg.Any<string>(),
            eventType:         Arg.Any<string>(),
            message:           Arg.Any<string>(),
            details:           Arg.Any<object?>(),
            agvSpeedMms:       Arg.Any<int?>(),
            batteryPercentage: Arg.Any<int?>(),
            positionX:         Arg.Any<double?>(),
            positionY:         Arg.Any<double?>(),
            pathId:            Arg.Any<long?>(),
            detectionId:       Arg.Any<long?>(),
            ct:                Arg.Any<CancellationToken>());

        // Target kept → next tick is Spinning (retry), not Idle
        modbus.ClearReceivedCalls();
        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns([waypoint]); // path clear now

        await orch.TickAsync(NoCancellation);

        // Should NOT send EmergencyStop or remain silent — should spin to re-align
        await modbus.DidNotReceive().WriteMotorCommandAsync(0, 0, CommandCode.EmergencyStop);
    }

    // =======================================================================
    // Scenario 5 — Replan cooldown: two blocked ticks → only 1 replan
    // =======================================================================

    [Fact]
    public async Task ReplanCooldown_TwoBlockedTicksInARow_ShouldOnlyReplanOnce()
    {
        var waypoint = new Point(5, 2);
        var agvState = AgvAtCell(2, 2, headingDeg: 0);

        var (orch, modbus, vision, planner, _) = Build(agvState);

        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns([waypoint]);

        orch.StartTrip(2750, 1250);

        // Tick 1: Spinning → aligned → Moving
        await orch.TickAsync(NoCancellation);

        // Inject persistent obstacle
        vision.GetLatestDetectionsAsync().Returns(new VisionResponse
        {
            Detections = [new Detection { ObjectClass = "box", Confidence = 0.9, DistanceMeters = 1.2 }],
            TotalObjects = 1,
        });

        // Tick 2: Moving — blocked → replan (FindPath call #2)
        await orch.TickAsync(NoCancellation);

        // Tick 3: Still blocked — cooldown not elapsed (< 500ms in same test run)
        await orch.TickAsync(NoCancellation);

        // FindPath should have been called exactly TWICE:
        // #1 = initial StartTrip, #2 = first replan, #3 would be cooldown violation
        planner.Received(2).FindPath(
            Arg.Any<GridMap>(),
            Arg.Any<Point>(),
            Arg.Any<Point>());
    }

    // =======================================================================
    // Scenario 6 — EmergencyStop mid-trip → motors cut immediately
    // =======================================================================

    [Fact]
    public async Task EmergencyStop_MidTrip_ShouldCutMotorsImmediately()
    {
        var waypoint = new Point(5, 2);
        var agvState = AgvAtCell(2, 2, headingDeg: 0);

        var (orch, modbus, _, planner, _) = Build(agvState);

        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns([waypoint]);

        orch.StartTrip(2750, 1250);

        // Tick 1: Spinning → aligned → Moving
        await orch.TickAsync(NoCancellation);

        // Tick 2: Moving forward
        await orch.TickAsync(NoCancellation);

        // Emergency stop called from HTTP thread (simulated inline)
        orch.EmergencyStop();
        await Task.Delay(50); // allow fire-and-forget to complete

        // EmergencyStop command sent
        await modbus.Received().WriteMotorCommandAsync(0, 0, CommandCode.EmergencyStop);

        // Tick 3: state is Idle → no more motor commands
        modbus.ClearReceivedCalls();
        await orch.TickAsync(NoCancellation);

        await modbus.DidNotReceive().WriteMotorCommandAsync(
            Arg.Any<short>(), Arg.Any<short>(), Arg.Any<CommandCode>());
    }
}
