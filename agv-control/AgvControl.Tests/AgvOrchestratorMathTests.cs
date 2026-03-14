// ==========================================================================
// AgvOrchestratorMathTests.cs — Layer 2: Internal math tests
// ==========================================================================
// Tests pure math helpers inside AgvOrchestrator via InternalsVisibleTo.
// AssemblyInfo.cs already has: [assembly: InternalsVisibleTo("AgvControl.Tests")]
//
// Methods under test (all static, no side effects, no mocks needed):
//   - ComputeTargetAngle(Point waypoint, AgvState agvState)
//   - NormalizeAngle(double deg)
//   - DistanceToWaypoint(Point waypoint, AgvState agvState)
//
// Why test these directly:
//   - Pure math — no DI, no network, no DB
//   - Bugs here = wrong motor direction or AGV overshooting waypoint
//   - Fast to run, easy to reason about
// ==========================================================================

using System.Drawing;
using AgvControl.Data;
using AgvControl.Models;
using AgvControl.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgvControl.Tests;

public class AgvOrchestratorMathTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Build a minimal orchestrator just to access internal static methods.
    /// No real services needed — all mocked.
    /// </summary>
    private static AgvOrchestrator BuildOrchestrator()
    {
        var modbus  = Substitute.For<IModbusClient>();
        var vision  = Substitute.For<IVisionClient>();
        var planner = Substitute.For<IPathPlanner>();
        var db      = Substitute.For<IDbLogger>();

        modbus.ReadStatusAsync().Returns(new AgvState());
        vision.GetLatestDetectionsAsync().Returns((VisionResponse?)null);
        planner.FindPath(Arg.Any<GridMap>(), Arg.Any<Point>(), Arg.Any<Point>())
               .Returns([]);

        return new AgvOrchestrator(vision, modbus, planner, db,
                                   NullLogger<AgvOrchestrator>.Instance);
    }

    /// <summary>AGV at given mm position, heading 0°.</summary>
    private static AgvState AgvAt(int xMm, int yMm, double headingDeg = 0) => new()
    {
        PositionX      = xMm,
        PositionY      = yMm,
        HeadingDegrees = headingDeg,
    };

    /// <summary>
    /// Waypoint cell center in mm = cell * 500 + 250.
    /// E.g. cell (3,2) center = (1750, 1250).
    /// </summary>
    private static Point WaypointCell(int gx, int gy) => new(gx, gy);

    // =======================================================================
    // NormalizeAngle
    // =======================================================================

    [Theory]
    [InlineData(0,     0)]       // already normalized
    [InlineData(180,   180)]     // boundary: +180 stays +180
    [InlineData(-180, -180)]     // boundary: -180 stays -180
    [InlineData(181,  -179)]     // just over +180 → wraps negative
    [InlineData(-181,  179)]     // just under -180 → wraps positive
    [InlineData(360,   0)]       // full circle → 0
    [InlineData(370,   10)]      // 360 + 10
    [InlineData(-370, -10)]      // -360 - 10
    [InlineData(540,   180)]     // 1.5 circles
    [InlineData(-540, -180)]     // -1.5 circles
    [InlineData(720,   0)]       // 2 full circles
    public void NormalizeAngle_VariousInputs_ShouldBeInRange(double input, double expected)
    {
        // NormalizeAngle is internal static — access via reflection helper
        var result = InvokeNormalizeAngle(input);

        Assert.Equal(expected, result, precision: 5);
    }

    [Fact]
    public void NormalizeAngle_Result_ShouldAlwaysBeInMinus180To180()
    {
        // Fuzz: try 720 values from -360 to +360
        for (int i = -360; i <= 360; i++)
        {
            double result = InvokeNormalizeAngle(i);
            Assert.True(result >= -180.0 && result <= 180.0,
                $"NormalizeAngle({i}) = {result} is outside -180..180");
        }
    }

    // =======================================================================
    // ComputeTargetAngle
    // =======================================================================

    [Fact]
    public void ComputeTargetAngle_WaypointDueEast_ShouldReturn0Degrees()
    {
        // AGV at (750, 750) mm (cell 1,1 center area)
        // Waypoint at grid (3,1) → center = (1750, 750) mm
        // dx = +1000, dy = 0 → atan2(0, 1000) = 0°
        var agv      = AgvAt(750, 750);
        var waypoint = WaypointCell(3, 1);

        double angle = InvokeComputeTargetAngle(waypoint, agv);

        Assert.Equal(0.0, angle, precision: 3);
    }

    [Fact]
    public void ComputeTargetAngle_WaypointDueNorth_ShouldReturnMinus90Degrees()
    {
        // AGV at (1250, 1750) mm
        // Waypoint at grid (2,1) → center = (1250, 750)
        // dx = 0, dy = -1000 → atan2(-1000, 0) = -90°
        var agv      = AgvAt(1250, 1750);
        var waypoint = WaypointCell(2, 1);

        double angle = InvokeComputeTargetAngle(waypoint, agv);

        Assert.Equal(-90.0, angle, precision: 3);
    }

    [Fact]
    public void ComputeTargetAngle_WaypointDueSouth_ShouldReturn90Degrees()
    {
        // AGV at (1250, 750) mm
        // Waypoint at grid (2,3) → center = (1250, 1750)
        // dx = 0, dy = +1000 → atan2(1000, 0) = +90°
        var agv      = AgvAt(1250, 750);
        var waypoint = WaypointCell(2, 3);

        double angle = InvokeComputeTargetAngle(waypoint, agv);

        Assert.Equal(90.0, angle, precision: 3);
    }

    [Fact]
    public void ComputeTargetAngle_WaypointDueWest_ShouldReturn180Degrees()
    {
        // AGV at (2750, 750) mm
        // Waypoint at grid (1,1) → center = (750, 750)
        // dx = -2000, dy = 0 → atan2(0, -2000) = ±180°
        var agv      = AgvAt(2750, 750);
        var waypoint = WaypointCell(1, 1);

        double angle = InvokeComputeTargetAngle(waypoint, agv);

        // atan2 returns +180 or -180 for due-west — both correct
        Assert.True(Math.Abs(Math.Abs(angle) - 180.0) < 0.001,
            $"Expected ±180°, got {angle}");
    }

    [Fact]
    public void ComputeTargetAngle_WaypointDiagonalNE_ShouldReturn45Degrees()
    {
        // AGV at (750, 1750) mm
        // Waypoint at grid (3,1) → center = (1750, 750)
        // dx = +1000, dy = -1000 → atan2(-1000, 1000) = -45°
        var agv      = AgvAt(750, 1750);
        var waypoint = WaypointCell(3, 1);

        double angle = InvokeComputeTargetAngle(waypoint, agv);

        Assert.Equal(-45.0, angle, precision: 3);
    }

    // =======================================================================
    // DistanceToWaypoint
    // =======================================================================

    [Fact]
    public void DistanceToWaypoint_AgvAtWaypointCenter_ShouldBeZero()
    {
        // AGV exactly at cell (3,2) center = (1750, 1250)
        var agv      = AgvAt(1750, 1250);
        var waypoint = WaypointCell(3, 2);

        double dist = InvokeDistanceToWaypoint(waypoint, agv);

        Assert.Equal(0.0, dist, precision: 3);
    }

    [Fact]
    public void DistanceToWaypoint_OneCell_ShouldBe500mm()
    {
        // AGV at cell (2,2) center = (1250, 1250)
        // Waypoint at cell (3,2) center = (1750, 1250)
        // distance = 500mm
        var agv      = AgvAt(1250, 1250);
        var waypoint = WaypointCell(3, 2);

        double dist = InvokeDistanceToWaypoint(waypoint, agv);

        Assert.Equal(500.0, dist, precision: 3);
    }

    [Fact]
    public void DistanceToWaypoint_DiagonalOneCell_ShouldBeCorrect()
    {
        // AGV at (1250, 1250), Waypoint center = (1750, 1750)
        // Expected = sqrt(500² + 500²) ≈ 707.107mm
        var agv      = AgvAt(1250, 1250);
        var waypoint = WaypointCell(3, 3);

        double dist = InvokeDistanceToWaypoint(waypoint, agv);

        Assert.Equal(Math.Sqrt(500 * 500 + 500 * 500), dist, precision: 3);
    }

    [Fact]
    public void DistanceToWaypoint_ShouldBeSymmetric()
    {
        // distance(A→B) == distance(B→A)
        var agvA = AgvAt(1250, 1250);
        var agvB = AgvAt(3750, 2750);
        var wpA  = WaypointCell(2, 2);  // center = (1250, 1250)
        var wpB  = WaypointCell(7, 5);  // center = (3750, 2750)

        double distAB = InvokeDistanceToWaypoint(wpB, agvA);
        double distBA = InvokeDistanceToWaypoint(wpA, agvB);

        Assert.Equal(distAB, distBA, precision: 3);
    }

    [Fact]
    public void DistanceToWaypoint_ShouldNeverBeNegative()
    {
        var agv      = AgvAt(5000, 3000);
        var waypoint = WaypointCell(2, 2);

        double dist = InvokeDistanceToWaypoint(waypoint, agv);

        Assert.True(dist >= 0, $"Distance should never be negative, got {dist}");
    }

    // =======================================================================
    // Integration: ComputeTargetAngle + NormalizeAngle together
    // (simulates what HandleSpinningAsync does every tick)
    // =======================================================================

    [Fact]
    public void SpinDecision_WaypointBehindAGV_ShouldRequireLargeTurn()
    {
        // AGV facing East (0°), waypoint is due West
        // → angleDiff should be ±180°, AGV must spin
        var agv      = AgvAt(2750, 1250, headingDeg: 0);
        var waypoint = WaypointCell(1, 2);  // center = (750, 1250) — due West

        double targetAngle = InvokeComputeTargetAngle(waypoint, agv);
        double angleDiff   = InvokeNormalizeAngle(targetAngle - agv.HeadingDegrees);

        Assert.True(Math.Abs(angleDiff) >= 170.0,
            $"Expected large turn (~180°), got angleDiff={angleDiff}");
    }

    [Fact]
    public void SpinDecision_AlreadyAligned_ShouldBeWithinThreshold()
    {
        // AGV facing East (0°), waypoint is due East
        // → angleDiff should be ~0°, within 10° threshold → no spin needed
        var agv      = AgvAt(1250, 1250, headingDeg: 0);
        var waypoint = WaypointCell(5, 2);  // due East

        double targetAngle = InvokeComputeTargetAngle(waypoint, agv);
        double angleDiff   = InvokeNormalizeAngle(targetAngle - agv.HeadingDegrees);

        Assert.True(Math.Abs(angleDiff) < 10.0,
            $"Expected aligned (< 10°), got angleDiff={angleDiff}");
    }

    // =======================================================================
    // Reflection helpers — invoke internal static methods
    // =======================================================================

    private static double InvokeNormalizeAngle(double deg)
    {
        var method = typeof(AgvOrchestrator).GetMethod(
            "NormalizeAngle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("NormalizeAngle not found");

        return (double)method.Invoke(null, [deg])!;
    }

    private static double InvokeComputeTargetAngle(Point waypoint, AgvState agvState)
    {
        var method = typeof(AgvOrchestrator).GetMethod(
            "ComputeTargetAngle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("ComputeTargetAngle not found");

        return (double)method.Invoke(null, [waypoint, agvState])!;
    }

    private static double InvokeDistanceToWaypoint(Point waypoint, AgvState agvState)
    {
        var method = typeof(AgvOrchestrator).GetMethod(
            "DistanceToWaypoint",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("DistanceToWaypoint not found");

        return (double)method.Invoke(null, [waypoint, agvState])!;
    }
}
