// ==========================================================================
// PathPlannerTests.cs — Unit tests for PathPlanner (A* on 40×20 grid)
// ==========================================================================
// Coverage:
//   1.  Trivial case          — start == goal → empty list
//   2.  Straight path         — clear horizontal / vertical corridor
//   3.  Path around wall      — static wall forces detour
//   4.  DynamicObstacle cost  — prefers empty path over obstacle path
//   5.  DynamicObstacle passable — can path through if no other route
//   6.  StaticWall impassable — never steps on StaticWall
//   7.  No path exists        — throws InvalidOperationException
//   8.  Path excludes start   — first element is NOT start
//   9.  Path ends at goal     — last element IS goal
//   10. Tie-breaker (straight)— prefers straight path over zigzag
//   11. Custom options        — PathPlannerOptions wired correctly
// ==========================================================================

using System.Drawing;
using AgvControl.Models;
using AgvControl.Services;

namespace AgvControl.Tests;

public class PathPlannerTests
{
    // -----------------------------------------------------------------------
    // Helper — build default PathPlanner (EmptyCost=1, DynamicObstacleCost=10)
    // -----------------------------------------------------------------------
    private static PathPlanner BuildPlanner(int emptyCost = 1, int dynamicCost = 10)
        => new(new PathPlannerOptions
        {
            EmptyCost            = emptyCost,
            DynamicObstacleCost  = dynamicCost,
        });

    /// <summary>Build an empty 40×20 map (no walls, no obstacles).</summary>
    private static GridMap EmptyMap() => new GridMap();

    /// <summary>Build a map with perimeter walls + example shelves.</summary>
    private static GridMap WalledMap()
    {
        var map = new GridMap();
        map.InitStaticWalls();
        return map;
    }

    // =====================================================================
    // 1. Trivial case — start == goal
    // =====================================================================

    [Fact]
    public void FindPath_StartEqualsGoal_ShouldReturnEmptyList()
    {
        var planner = BuildPlanner();
        var map     = EmptyMap();
        var start   = new Point(5, 5);

        var path = planner.FindPath(map, start, start);

        Assert.Empty(path);
    }

    // =====================================================================
    // 2. Straight horizontal path — unobstructed
    // =====================================================================

    [Fact]
    public void FindPath_HorizontalCorridor_ShouldReturnCorrectLength()
    {
        var planner = BuildPlanner();
        var map     = EmptyMap();
        var start   = new Point(5, 5);
        var goal    = new Point(10, 5);     // 5 steps East

        var path = planner.FindPath(map, start, goal);

        Assert.Equal(5, path.Count);        // 5 steps, start excluded
        Assert.Equal(goal, path[^1]);       // last element = goal
    }

    [Fact]
    public void FindPath_VerticalCorridor_ShouldReturnCorrectLength()
    {
        var planner = BuildPlanner();
        var map     = EmptyMap();
        var start   = new Point(3, 2);
        var goal    = new Point(3, 8);      // 6 steps South

        var path = planner.FindPath(map, start, goal);

        Assert.Equal(6, path.Count);
        Assert.Equal(goal, path[^1]);
    }

    // =====================================================================
    // 3. Path around static wall
    // =====================================================================

    [Fact]
    public void FindPath_StaticWallBlocking_ShouldDetourAroundIt()
    {
        var planner = BuildPlanner();
        var map = EmptyMap();

        // true wall
        for (int y = 0; y <= 9; y++)
            map.SetStaticWall(5, y);

        var start = new Point(3, 5);
        var goal = new Point(7, 5);

        var path = planner.FindPath(map, start, goal);

        Assert.NotEmpty(path);
        Assert.Equal(goal, path[^1]);

        // path must not step on wall cells
        Assert.DoesNotContain(path, p => p.X == 5 && p.Y <= 9);
    }

    // =====================================================================
    // 4. DynamicObstacle — prefers detour over going through obstacle
    // =====================================================================

    [Fact]
    public void FindPath_DynamicObstacleInDirectRoute_ShouldPreferCleanDetour()
    {
        var planner = BuildPlanner(emptyCost: 1, dynamicCost: 10);
        var map     = EmptyMap();

        // Place obstacle at (5,5) — directly on the shortest horizontal path
        map.SetObstacle(5, 5);

        var start = new Point(3, 5);
        var goal  = new Point(7, 5);

        var path = planner.FindPath(map, start, goal);

        // A* should route around (5,5) because detour cost < obstacle cost
        Assert.Equal(goal, path[^1]);
        Assert.DoesNotContain(path, p => p.X == 5 && p.Y == 5);
    }

    // =====================================================================
    // 5. DynamicObstacle passable when no other route exists
    // =====================================================================

    [Fact]
    public void FindPath_DynamicObstacleOnlyRoute_ShouldPathThroughIt()
    {
        var planner = BuildPlanner(emptyCost: 1, dynamicCost: 10);
        var map     = EmptyMap();

        // Seal a corridor with dynamic obstacles on both sides — only route is through
        // Narrow 1-cell wide corridor at y=5, blocked above (y=4) and below (y=6)
        // with static walls we simulate via obstacles everywhere EXCEPT x=3..7, y=5
        // Simpler: create a single-cell bottleneck at (5,5) — only passable point
        for (int y = 0; y < GridMap.Height; y++)
        {
            if (y != 5)
            {
                map.SetObstacle(5, y);      // block entire column x=5 except row y=5
            }
        }
        // (5,5) is DynamicObstacle → A* must pass through it
        map.SetObstacle(5, 5);

        // Route must cross x=5 — all cells at x=5 are obstacles → forced through (5,5)
        var start = new Point(3, 5);
        var goal  = new Point(7, 5);

        var path = planner.FindPath(map, start, goal);

        Assert.Equal(goal, path[^1]);
        // Path must include (5,5) since that's the only crossing
        Assert.Contains(path, p => p.X == 5 && p.Y == 5);
    }

    // =====================================================================
    // 6. StaticWall — A* must never step on it
    // =====================================================================

    [Fact]
    public void FindPath_ResultPath_ShouldNeverContainStaticWall()
    {
        var planner = BuildPlanner();
        var map     = WalledMap();  // has perimeter walls + shelves

        // Interior cells well within the 40×20 grid (away from perimeter and shelves)
        var start = new Point(5, 5);
        var goal  = new Point(15, 15);

        var path = planner.FindPath(map, start, goal);

        Assert.NotEmpty(path);
        foreach (var cell in path)
            Assert.NotEqual(CellType.StaticWall, map.GetCell(cell.X, cell.Y));
    }

    // =====================================================================
    // 7. No path — completely blocked → throws InvalidOperationException
    // =====================================================================

    [Fact]
    public void FindPath_CompletelyBlocked_ShouldThrowInvalidOperationException()
    {
        var planner = BuildPlanner();
        var map     = EmptyMap();

        // Surround start (5,5) on all 4 sides with obstacles
        map.SetObstacle(5, 4);   // North
        map.SetObstacle(5, 6);   // South
        map.SetObstacle(6, 5);   // East
        map.SetObstacle(4, 5);   // West

        var start = new Point(5, 5);
        var goal  = new Point(15, 15);

        // Dynamic obstacles are passable (cost=10) → AGV can still push through them.
        // To guarantee no path, use a map where start is surrounded by StaticWalls.
        // We simulate this by building a walled map with start isolated.

        // ---- Isolated start with StaticWall via custom subgrid approach ----
        // Simplest valid setup: goal is inside a StaticWall ring.
        // Use InitStaticWalls map then choose goal inside an enclosed shelf area.
        var walledMap = WalledMap();
        // Shelf 1 is at x=10, y=3..8 (single column). Goal inside shelf column:
        var blockedGoal = new Point(10, 5);   // inside the shelf StaticWall column

        // Start outside shelf, goal ON StaticWall cell → unreachable
        var ex = Assert.Throws<InvalidOperationException>(
            () => planner.FindPath(walledMap, new Point(5, 5), blockedGoal)
        );
        Assert.Contains("no path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // =====================================================================
    // 8. Path excludes start node
    // =====================================================================

    [Fact]
    public void FindPath_ResultPath_ShouldNotContainStartNode()
    {
        var planner = BuildPlanner();
        var map     = EmptyMap();
        var start   = new Point(2, 2);
        var goal    = new Point(6, 2);

        var path = planner.FindPath(map, start, goal);

        Assert.DoesNotContain(start, path);
    }

    // =====================================================================
    // 9. Path ends at goal
    // =====================================================================

    [Fact]
    public void FindPath_LastElement_ShouldBeGoal()
    {
        var planner = BuildPlanner();
        var map     = EmptyMap();
        var start   = new Point(1, 1);
        var goal    = new Point(8, 8);

        var path = planner.FindPath(map, start, goal);

        Assert.Equal(goal, path[^1]);
    }

    // =====================================================================
    // 10. Tie-breaker — straight path preferred over zigzag
    // =====================================================================

    [Fact]
    public void FindPath_OpenField_ShouldReturnStraightHorizontalPath()
    {
        // On a completely open map, 4-dir A* with Manhattan + tie-breaker
        // should return a straight horizontal path (no vertical deviation).
        var planner = BuildPlanner();
        var map     = EmptyMap();
        var start   = new Point(5, 10);
        var goal    = new Point(15, 10);    // same row

        var path = planner.FindPath(map, start, goal);

        // Every cell must stay on y=10 (no zigzag)
        Assert.All(path, p => Assert.Equal(10, p.Y));
    }

    [Fact]
    public void FindPath_OpenField_ShouldReturnStraightVerticalPath()
    {
        var planner = BuildPlanner();
        var map     = EmptyMap();
        var start   = new Point(10, 3);
        var goal    = new Point(10, 12);    // same column

        var path = planner.FindPath(map, start, goal);

        Assert.All(path, p => Assert.Equal(10, p.X));
    }

    // =====================================================================
    // 11. Custom PathPlannerOptions — high dynamic cost avoids obstacle
    // =====================================================================

    [Fact]
    public void FindPath_WithHighDynamicCost_ShouldStillAvoidObstacle()
    {
        // DynamicObstacleCost=100 — even stronger avoidance
        var planner = BuildPlanner(emptyCost: 1, dynamicCost: 100);
        var map     = EmptyMap();

        map.SetObstacle(5, 5);

        var start = new Point(3, 5);
        var goal  = new Point(7, 5);

        var path = planner.FindPath(map, start, goal);

        Assert.Equal(goal, path[^1]);
        Assert.DoesNotContain(path, p => p.X == 5 && p.Y == 5);
    }

    [Fact]
    public void FindPath_WithLowDynamicCost_ShouldAllowThroughObstacle()
    {
        // DynamicObstacleCost=1 (same as empty) — obstacle treated like empty cell
        var planner = BuildPlanner(emptyCost: 1, dynamicCost: 1);
        var map     = EmptyMap();

        // Block every path EXCEPT through (5,5) obstacle
        for (int y = 0; y < GridMap.Height; y++)
            if (y != 5) map.SetObstacle(5, y);
        map.SetObstacle(5, 5);

        var start = new Point(3, 5);
        var goal  = new Point(7, 5);

        var path = planner.FindPath(map, start, goal);

        Assert.Equal(goal, path[^1]);
        Assert.Contains(path, p => p.X == 5 && p.Y == 5);
    }
}
