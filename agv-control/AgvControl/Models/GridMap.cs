// ==========================================================================
// GridMap.cs — 40×20 warehouse grid for A* pathfinding
// ==========================================================================
// Warehouse: 20m × 10m, cell size: 500mm → grid 40×20
// Static walls defined at init + dynamic obstacles from Vision AI each cycle
// ==========================================================================

namespace AgvControl.Models;

/// <summary>
/// Cell types for the pathfinding grid.
/// </summary>
public enum CellType
{
    /// <summary>Passable — AGV can move through.</summary>
    Empty = 0,

    /// <summary>Fixed wall/shelf — defined at startup, never changes.</summary>
    StaticWall = 1,

    /// <summary>Vision AI detected obstacle — refreshed each control loop cycle.</summary>
    DynamicObstacle = 2,
}

/// <summary>
/// 2D grid representation of the warehouse for A* pathfinding.
/// 
/// Coordinate system:
///   - Origin (0,0) = top-left corner of warehouse
///   - X axis = horizontal (0 → 20000mm, 0 → 40 cells)
///   - Y axis = vertical   (0 → 10000mm, 0 → 20 cells)
///   - Grid[x, y] = cell type
/// </summary>
public class GridMap
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------
    public const int Width = 40;            // 20000mm / 500mm
    public const int Height = 20;           // 10000mm / 500mm
    public const int CellSizeMm = 500;      // 500mm per cell

    // -----------------------------------------------------------------------
    // Grid data
    // -----------------------------------------------------------------------
    private readonly CellType[,] _grid = new CellType[Width, Height];

    /// <summary>Read-only access to grid for pathfinding.</summary>
    public CellType GetCell(int x, int y) => _grid[x, y];

    /// <summary>Get flat array for JSON serialization (row-major).</summary>
    public int[,] ToArray()
    {
        var result = new int[Width, Height];
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                result[x, y] = (int)_grid[x, y];
        return result;
    }

    // -----------------------------------------------------------------------
    // Initialization — static warehouse layout
    // -----------------------------------------------------------------------

    /// <summary>
    /// Define static walls (warehouse perimeter + shelves).
    /// Called once at startup.
    /// </summary>
    public void InitStaticWalls()
    {
        // Warehouse perimeter walls
        for (int x = 0; x < Width; x++)
        {
            _grid[x, 0] = CellType.StaticWall;            // Top wall
            _grid[x, Height - 1] = CellType.StaticWall;   // Bottom wall
        }
        for (int y = 0; y < Height; y++)
        {
            _grid[0, y] = CellType.StaticWall;            // Left wall
            _grid[Width - 1, y] = CellType.StaticWall;    // Right wall
        }

        // Example shelves (2 vertical shelves in warehouse)
        // Shelf 1: x=10, y=3 to y=8
        for (int y = 3; y <= 8; y++)
            _grid[10, y] = CellType.StaticWall;

        // Shelf 2: x=25, y=10 to y=16
        for (int y = 10; y <= 16; y++)
            _grid[25, y] = CellType.StaticWall;
    }

    // -----------------------------------------------------------------------
    // Dynamic obstacles — refreshed each control loop cycle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Clear all dynamic obstacles. Called at the start of each control loop
    /// before mapping new Vision AI detections.
    /// Static walls are NOT cleared.
    /// </summary>
    public void ClearDynamicObstacles()
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_grid[x, y] == CellType.DynamicObstacle)
                    _grid[x, y] = CellType.Empty;
    }

    /// <summary>
    /// Mark a cell as dynamic obstacle.
    /// IMPORTANT: Always performs bounds check to prevent IndexOutOfRangeException.
    /// </summary>
    /// <param name="x">Grid X coordinate (0 to Width-1).</param>
    /// <param name="y">Grid Y coordinate (0 to Height-1).</param>
    /// <returns>True if obstacle was set, false if out of bounds.</returns>
    public bool SetObstacle(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        // Don't overwrite static walls
        if (_grid[x, y] == CellType.StaticWall)
            return false;

        _grid[x, y] = CellType.DynamicObstacle;
        return true;
    }

    // -----------------------------------------------------------------------
    // Coordinate conversion — world (mm) to grid
    // -----------------------------------------------------------------------

    /// <summary>
    /// Convert world coordinates (mm) to grid cell indices.
    /// Returns (-1, -1) if out of bounds.
    /// </summary>
    public (int gridX, int gridY) WorldToGrid(double xMm, double yMm)
    {
        int gx = (int)(xMm / CellSizeMm);
        int gy = (int)(yMm / CellSizeMm);

        if (gx < 0 || gx >= Width || gy < 0 || gy >= Height)
            return (-1, -1);

        return (gx, gy);
    }

    /// <summary>
    /// Check if a grid cell is walkable (not wall or obstacle).
    /// Returns false for out-of-bounds coordinates.
    /// </summary>
    public bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        return _grid[x, y] == CellType.Empty;
    }
}
