// ==========================================================================
// PathPlanner.cs — A* pathfinding on 40×20 warehouse grid
// ==========================================================================
// Algorithm  : A* with 4-directional movement (N/S/E/W)
// Heuristic  : Manhattan distance (admissible for 4-dir)
// Tie-breaker: prefer node with smaller h (straighter path, less zigzag)
// Costs      : Empty = PathPlannerOptions.EmptyCost (default 1)
//              DynamicObstacle = PathPlannerOptions.DynamicObstacleCost (default 10)
//              StaticWall = impassable
// No path    : throws InvalidOperationException (Orchestrator handles)
// ==========================================================================

using System.Drawing;
using AgvControl.Models;

namespace AgvControl.Services;

// ---------------------------------------------------------------------------
// Options — injected via DI, avoids hardcoded cost magic numbers
// ---------------------------------------------------------------------------

/// <summary>
/// Tunable cost parameters for A* pathfinding.
/// Bind from appsettings.json: "PathPlanner" section.
/// </summary>
public class PathPlannerOptions
{
    /// <summary>Movement cost for an empty cell. Default: 1.</summary>
    public int EmptyCost { get; init; } = 1;

    /// <summary>
    /// Movement cost for a DynamicObstacle cell.
    /// Higher than EmptyCost so A* avoids obstacles when possible,
    /// but can still path through them if no other route exists.
    /// Default: 10.
    /// </summary>
    public int DynamicObstacleCost { get; init; } = 10;
}

// ---------------------------------------------------------------------------
// Interface
// ---------------------------------------------------------------------------

public interface IPathPlanner
{
    /// <summary>
    /// Find shortest path from <paramref name="start"/> to <paramref name="goal"/>
    /// on the given <paramref name="map"/> using A*.
    /// </summary>
    /// <returns>
    /// Ordered list of grid cells from start (exclusive) to goal (inclusive).
    /// Returns empty list if start == goal.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no path exists (completely blocked).
    /// </exception>
    List<Point> FindPath(GridMap map, Point start, Point goal);
}

// ---------------------------------------------------------------------------
// Implementation
// ---------------------------------------------------------------------------

public class PathPlanner : IPathPlanner
{
    // 4-directional movement: N, S, E, W
    private static readonly (int dx, int dy)[] Directions =
    [
        ( 0, -1),   // North
        ( 0,  1),   // South
        ( 1,  0),   // East
        (-1,  0),   // West
    ];

    private readonly PathPlannerOptions _options;

    public PathPlanner(PathPlannerOptions options)
    {
        _options = options;
    }

    /// <inheritdoc/>
    public List<Point> FindPath(GridMap map, Point start, Point goal)
    {
        // ---- trivial case ----
        if (start == goal)
            return [];

        // ---- A* data structures ----
        // Priority: (f, h) — tie-break on h to prefer nodes closer to goal
        // (smaller h = more direct path = less zigzag)
        var openSet = new PriorityQueue<Point, (int f, int h)>(
            Comparer<(int f, int h)>.Create((a, b) =>
                a.f != b.f ? a.f.CompareTo(b.f) : a.h.CompareTo(b.h)
            )
        );

        var gScore  = new Dictionary<Point, int>();     // cost from start
        var cameFrom = new Dictionary<Point, Point>();  // for path reconstruction

        gScore[start] = 0;
        int hStart = Manhattan(start, goal);
        openSet.Enqueue(start, (hStart, hStart));

        // ---- main loop ----
        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            // Reached goal — reconstruct path
            if (current == goal)
                return ReconstructPath(cameFrom, current);

            foreach (var (dx, dy) in Directions)
            {
                var neighbor = new Point(current.X + dx, current.Y + dy);

                int moveCost = CellMoveCost(map, neighbor);
                if (moveCost < 0)
                    continue;   // impassable (StaticWall or out of bounds)

                int tentativeG = gScore[current] + moveCost;

                if (gScore.TryGetValue(neighbor, out int existingG) && tentativeG >= existingG)
                    continue;   // already found a better path to this neighbor

                cameFrom[neighbor] = current;
                gScore[neighbor]   = tentativeG;

                int h = Manhattan(neighbor, goal);
                openSet.Enqueue(neighbor, (tentativeG + h, h));
            }
        }

        // Open set exhausted — no path exists
        throw new InvalidOperationException(
            $"A* found no path from ({start.X},{start.Y}) to ({goal.X},{goal.Y}). " +
            "Target may be blocked or unreachable."
        );
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Returns movement cost to enter <paramref name="cell"/>.
    /// Returns -1 if the cell is impassable.
    /// </summary>
    private int CellMoveCost(GridMap map, Point cell)
    {
        var type = map.GetCell(cell.X, cell.Y);   // returns StaticWall for OOB

        return type switch
        {
            CellType.Empty           => _options.EmptyCost,
            CellType.DynamicObstacle => _options.DynamicObstacleCost,
            CellType.StaticWall      => -1,         // impassable
            _                        => _options.EmptyCost,   // AgvPosition → treat as Empty
        };
    }

    /// <summary>Manhattan distance heuristic — admissible for 4-directional movement.</summary>
    private static int Manhattan(Point a, Point b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    /// <summary>Reconstruct path by walking cameFrom chain from goal back to start.</summary>
    private static List<Point> ReconstructPath(Dictionary<Point, Point> cameFrom, Point current)
    {
        var path = new List<Point>();

        while (cameFrom.ContainsKey(current))
        {
            path.Add(current);
            current = cameFrom[current];
        }
        // 'current' is now start — exclude it (caller already knows start position)

        path.Reverse();
        return path;
    }
}
