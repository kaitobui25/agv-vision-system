// ==========================================================================
// AgvOrchestratorTests.cs — Layer 1: Public API tests
// ==========================================================================
// Tests the "contract" of AgvOrchestrator as seen by Controllers.
// No internal knowledge required — only public methods are called.
//
// Mock strategy (NSubstitute 5.3.0):
//   IModbusClient  — ReadStatusAsync returns a default AgvState
//   IVisionClient  — GetLatestDetectionsAsync returns null (no obstacles)
//   IPathPlanner   — FindPath returns a configurable list of waypoints
//   IDbLogger      — fire-and-forget, all calls ignored
//
// What is NOT tested here (covered in Layer 2 / Layer 3):
//   - Internal steering math (ComputeTargetAngle, NormalizeAngle)
//   - Tick-by-tick state transitions (Spinning → Moving → Idle)
// ==========================================================================

using System.Drawing;
using AgvControl.Data;
using AgvControl.Models;
using AgvControl.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AgvControl.Tests;

public class AgvOrchestratorTests
{
    // -----------------------------------------------------------------------
    // Helpers — build orchestrator with sensible defaults
    // -----------------------------------------------------------------------

    /// <summary>
    /// AGV sitting at (1250, 1250) mm = grid cell (2, 2), heading 0°, battery 100%.
    /// Well inside the 40×20 warehouse, away from perimeter walls.
    /// </summary>
    private static AgvState DefaultAgvState() => new()
    {
        PositionX      = 1250,
        PositionY      = 1250,
        HeadingDegrees = 0,
        BatteryLevel   = 100,
        Status         = StatusCode.Idle,
        Error          = ErrorCode.Ok,
    };

    /// <summary>
    /// A simple 3-waypoint path from grid (2,2) to (5,2).
    /// Used as default FindPath return value.
    /// </summary>
    private static List<Point> SimpleThreeWaypoints() =>
    [
        new Point(3, 2),
        new Point(4, 2),
        new Point(5, 2),
    ];

    private static AgvOrchestrator BuildOrchestrator(
        IModbusClient?  modbus   = null,
        IVisionClient?  vision   = null,
        IPathPlanner?   planner  = null,
        IDbLogger?      db       = null,
        AgvState?       agvState = null,
        List<Point>?    path     = null)
    {
        modbus  ??= Substitute.For<IModbusClient>();
        vision  ??= Substitute.For<IVisionClient>();
        planner ??= Substitute.For<IPathPlanner>();
        db      ??= Substitute.For<IDbLogger>();

        modbus.ReadStatusAsync().Returns(agvState ?? DefaultAgvState());
        vision.GetLatestDetectionsAsync().Returns((VisionResponse?)null);
        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns(path ?? SimpleThreeWaypoints());

        return new AgvOrchestrator(vision, modbus, planner, db,
                                   NullLogger<AgvOrchestrator>.Instance);
    }

    // =======================================================================
    // 1. GetCurrentState — default state before any trip
    // =======================================================================

    [Fact]
    public void GetCurrentState_BeforeAnyTrip_ShouldReturnDefaultState()
    {
        var orchestrator = BuildOrchestrator();

        var state = orchestrator.GetCurrentState();

        // Freshly constructed — position is 0,0, status Idle
        Assert.Equal(StatusCode.Idle, state.Status);
        Assert.Equal(0, state.PositionX);
        Assert.Equal(0, state.PositionY);
    }

    // =======================================================================
    // 2. GetCurrentState — returns a COPY, not internal reference
    // =======================================================================

    [Fact]
    public void GetCurrentState_ShouldReturnCopy_NotReference()
    {
        var orchestrator = BuildOrchestrator();

        var state1 = orchestrator.GetCurrentState();
        var state2 = orchestrator.GetCurrentState();

        // Different object instances
        Assert.NotSame(state1, state2);
    }

    // =======================================================================
    // 3. GetCurrentMap — returns a COPY, not internal reference
    // =======================================================================

    [Fact]
    public void GetCurrentMap_ShouldReturnCopy_NotReference()
    {
        var orchestrator = BuildOrchestrator();

        var map1 = orchestrator.GetCurrentMap();
        var map2 = orchestrator.GetCurrentMap();

        Assert.NotSame(map1, map2);
    }

    // =======================================================================
    // 4. StartTrip — valid target → planner is called once
    // =======================================================================

    [Fact]
    public void StartTrip_ValidTarget_ShouldCallFindPathOnce()
    {
        var planner = Substitute.For<IPathPlanner>();
        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns(SimpleThreeWaypoints());

        var orchestrator = BuildOrchestrator(planner: planner);

        // Target at grid cell (5,2) = 2750mm, 1250mm
        orchestrator.StartTrip(2750, 1250);

        planner.Received(1).FindPath(
            Arg.Any<GridMap>(),
            Arg.Any<Point>(),
            Arg.Any<Point>());
    }

    // =======================================================================
    // 5. StartTrip — out-of-bounds target → planner NOT called, no exception
    // =======================================================================

    [Fact]
    public void StartTrip_OutOfBoundsTarget_ShouldNotCallFindPath()
    {
        var planner = Substitute.For<IPathPlanner>();
        var orchestrator = BuildOrchestrator(planner: planner);

        // X = 99999mm >> 20000mm warehouse width
        orchestrator.StartTrip(99999, 99999);

        planner.DidNotReceive().FindPath(
            Arg.Any<GridMap>(),
            Arg.Any<Point>(),
            Arg.Any<Point>());
    }

    // =======================================================================
    // 6. StartTrip — planner throws (no path) → no exception surfaces
    // =======================================================================

    [Fact]
    public void StartTrip_PlannerThrows_ShouldNotThrow()
    {
        var planner = Substitute.For<IPathPlanner>();
        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Throws(new InvalidOperationException("no path found"));

        var orchestrator = BuildOrchestrator(planner: planner);

        // Must not throw — orchestrator handles this gracefully
        var ex = Record.Exception(() => orchestrator.StartTrip(2750, 1250));
        Assert.Null(ex);
    }

    // =======================================================================
    // 7. StopTrip — always succeeds, sends Stop motor command
    // =======================================================================

    [Fact]
    public async Task StopTrip_ShouldSendStopMotorCommand()
    {
        var modbus = Substitute.For<IModbusClient>();
        modbus.ReadStatusAsync().Returns(DefaultAgvState());

        var orchestrator = BuildOrchestrator(modbus: modbus);
        orchestrator.StopTrip();

        // Allow fire-and-forget task to complete
        await Task.Delay(50);

        await modbus.Received().WriteMotorCommandAsync(0, 0, CommandCode.Stop);
    }

    // =======================================================================
    // 8. EmergencyStop — sends EmergencyStop motor command
    // =======================================================================

    [Fact]
    public async Task EmergencyStop_ShouldSendEmergencyStopCommand()
    {
        var modbus = Substitute.For<IModbusClient>();
        modbus.ReadStatusAsync().Returns(DefaultAgvState());

        var orchestrator = BuildOrchestrator(modbus: modbus);
        orchestrator.EmergencyStop();

        await Task.Delay(50);

        await modbus.Received().WriteMotorCommandAsync(0, 0, CommandCode.EmergencyStop);
    }

    // =======================================================================
    // 9. StartTrip then StopTrip — stop clears trip, no lingering state
    // =======================================================================

    [Fact]
    public async Task StartTripThenStop_ShouldSendStopCommand()
    {
        var modbus = Substitute.For<IModbusClient>();
        modbus.ReadStatusAsync().Returns(DefaultAgvState());

        var orchestrator = BuildOrchestrator(modbus: modbus);

        orchestrator.StartTrip(2750, 1250);
        orchestrator.StopTrip();

        await Task.Delay(50);

        await modbus.Received().WriteMotorCommandAsync(0, 0, CommandCode.Stop);
    }

    // =======================================================================
    // 10. Multiple StartTrip calls — last one wins, planner called each time
    // =======================================================================

    [Fact]
    public void StartTrip_CalledTwice_ShouldCallFindPathTwice()
    {
        var planner = Substitute.For<IPathPlanner>();
        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns(SimpleThreeWaypoints());

        var orchestrator = BuildOrchestrator(planner: planner);

        orchestrator.StartTrip(2750, 1250);
        orchestrator.StartTrip(3750, 2250);

        planner.Received(2).FindPath(
            Arg.Any<GridMap>(),
            Arg.Any<Point>(),
            Arg.Any<Point>());
    }

    // =======================================================================
    // 11. EmergencyStop after StartTrip — motor command is EmergencyStop
    // =======================================================================

    [Fact]
    public async Task EmergencyStop_AfterStartTrip_ShouldSendEmergencyStopNotStop()
    {
        var modbus = Substitute.For<IModbusClient>();
        modbus.ReadStatusAsync().Returns(DefaultAgvState());

        var orchestrator = BuildOrchestrator(modbus: modbus);

        orchestrator.StartTrip(2750, 1250);
        orchestrator.EmergencyStop();

        await Task.Delay(50);

        await modbus.Received().WriteMotorCommandAsync(0, 0, CommandCode.EmergencyStop);
        await modbus.DidNotReceive().WriteMotorCommandAsync(0, 0, CommandCode.Stop);
    }

    // =======================================================================
    // 12. StartTrip — DB LogPathAsync called once (fire-and-forget)
    // =======================================================================

    [Fact]
    public async Task StartTrip_ShouldCallLogPathAsync()
    {
        var db = Substitute.For<IDbLogger>();
        var orchestrator = BuildOrchestrator(db: db);

        orchestrator.StartTrip(2750, 1250);

        // Allow fire-and-forget to complete
        await Task.Delay(100);

        await db.Received(1).LogPathAsync(Arg.Any<PathRecord>());
    }

    // =======================================================================
    // 13. Concurrent calls — no exception from race condition
    // =======================================================================

    [Fact]
    public async Task ConcurrentStartTripAndStop_ShouldNotThrow()
    {
        var orchestrator = BuildOrchestrator();

        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            if (i % 2 == 0)
                orchestrator.StartTrip(2750, 1250);
            else
                orchestrator.StopTrip();
        }));

        var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(ex);
    }
}
