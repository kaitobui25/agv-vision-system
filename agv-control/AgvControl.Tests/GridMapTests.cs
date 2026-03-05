// ==========================================================================
// GridMapTests.cs — Unit tests for GridMap (40×20 warehouse grid)
// ==========================================================================
// Coverage: Constructor, GetCell, SetObstacle, IsWalkable,
//           InitStaticWalls, ClearDynamicObstacles, WorldToGrid, ToArray
// ==========================================================================

using AgvControl.Models;

namespace AgvControl.Tests;

public class GridMapTests
{
    // =====================================================================
    // 1. Constructor — All cells must be empty on initialization
    // =====================================================================

    [Fact]
    public void Constructor_AllCells_ShouldBeEmpty()
    {
        var map = new GridMap();

        for (int x = 0; x < GridMap.Width; x++)
            for (int y = 0; y < GridMap.Height; y++)
                Assert.Equal(CellType.Empty, map.GetCell(x, y));
    }

    // =====================================================================
    // 2. GetCell — Implicit Boundary Wall (bug fix regression test)
    // =====================================================================

    [Theory]
    [InlineData(-1, 0)]              // Negative X
    [InlineData(0, -1)]              // Negative Y
    [InlineData(-1, -1)]             // Both negative
    [InlineData(40, 0)]              // X = Width (off by 1)
    [InlineData(0, 20)]              // Y = Height (off by 1)
    [InlineData(40, 20)]             // Both exceed max
    [InlineData(100, 100)]           // Far beyond bounds
    [InlineData(-999, -999)]         // Far negative
    public void GetCell_OutOfBounds_ShouldReturnStaticWall(int x, int y)
    {
        var map = new GridMap();
        Assert.Equal(CellType.StaticWall, map.GetCell(x, y));
    }

    [Fact]
    public void GetCell_InBounds_ShouldReturnActualCellValue()
    {
        var map = new GridMap();
        // Empty cell
        Assert.Equal(CellType.Empty, map.GetCell(5, 5));

        // Set obstacle then read back
        map.SetObstacle(5, 5);
        Assert.Equal(CellType.DynamicObstacle, map.GetCell(5, 5));
    }

    // =====================================================================
    // 3. SetObstacle — Bounds check + must not overwrite static walls
    // =====================================================================

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, -1)]
    [InlineData(40, 10)]
    [InlineData(10, 20)]
    public void SetObstacle_OutOfBounds_ShouldReturnFalse(int x, int y)
    {
        var map = new GridMap();
        Assert.False(map.SetObstacle(x, y));
    }

    [Fact]
    public void SetObstacle_OnEmptyCell_ShouldSucceed()
    {
        var map = new GridMap();
        bool result = map.SetObstacle(15, 10);

        Assert.True(result);
        Assert.Equal(CellType.DynamicObstacle, map.GetCell(15, 10));
    }

    [Fact]
    public void SetObstacle_OnStaticWall_ShouldReturnFalse_AndNotOverwrite()
    {
        var map = new GridMap();
        map.InitStaticWalls();

        // Cell (0,0) is a perimeter wall after InitStaticWalls
        bool result = map.SetObstacle(0, 0);

        Assert.False(result);
        Assert.Equal(CellType.StaticWall, map.GetCell(0, 0)); // Wall unchanged
    }

    // =====================================================================
    // 4. IsWalkable — Bounds check + distinguish Empty vs Wall vs Obstacle
    // =====================================================================

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(40, 0)]
    [InlineData(0, 20)]
    public void IsWalkable_OutOfBounds_ShouldReturnFalse(int x, int y)
    {
        var map = new GridMap();
        Assert.False(map.IsWalkable(x, y));
    }

    [Fact]
    public void IsWalkable_EmptyCell_ShouldReturnTrue()
    {
        var map = new GridMap();
        Assert.True(map.IsWalkable(5, 5));
    }

    [Fact]
    public void IsWalkable_StaticWall_ShouldReturnFalse()
    {
        var map = new GridMap();
        map.InitStaticWalls();
        Assert.False(map.IsWalkable(0, 0)); // Perimeter wall
    }

    [Fact]
    public void IsWalkable_DynamicObstacle_ShouldReturnFalse()
    {
        var map = new GridMap();
        map.SetObstacle(10, 10);
        Assert.False(map.IsWalkable(10, 10));
    }

    // =====================================================================
    // 5. InitStaticWalls — Perimeter + Shelves
    // =====================================================================

    [Fact]
    public void InitStaticWalls_Perimeter_AllEdgeCells_ShouldBeStaticWall()
    {
        var map = new GridMap();
        map.InitStaticWalls();

        // Top wall (y=0) and Bottom wall (y=Height-1)
        for (int x = 0; x < GridMap.Width; x++)
        {
            Assert.Equal(CellType.StaticWall, map.GetCell(x, 0));
            Assert.Equal(CellType.StaticWall, map.GetCell(x, GridMap.Height - 1));
        }

        // Left wall (x=0) and Right wall (x=Width-1)
        for (int y = 0; y < GridMap.Height; y++)
        {
            Assert.Equal(CellType.StaticWall, map.GetCell(0, y));
            Assert.Equal(CellType.StaticWall, map.GetCell(GridMap.Width - 1, y));
        }
    }

    [Fact]
    public void InitStaticWalls_Shelves_ShouldBeStaticWall()
    {
        var map = new GridMap();
        map.InitStaticWalls();

        // Shelf 1: x=10, y=3..8
        for (int y = 3; y <= 8; y++)
            Assert.Equal(CellType.StaticWall, map.GetCell(10, y));

        // Shelf 2: x=25, y=10..16
        for (int y = 10; y <= 16; y++)
            Assert.Equal(CellType.StaticWall, map.GetCell(25, y));
    }

    [Fact]
    public void InitStaticWalls_InteriorNonShelfCells_ShouldRemainEmpty()
    {
        var map = new GridMap();
        map.InitStaticWalls();

        // Interior cells not on perimeter or shelf
        Assert.Equal(CellType.Empty, map.GetCell(20, 10));
        Assert.Equal(CellType.Empty, map.GetCell(5, 5));
    }

    // =====================================================================
    // 6. ClearDynamicObstacles — Remove obstacles, preserve static walls
    // =====================================================================

    [Fact]
    public void ClearDynamicObstacles_ShouldRemoveAllObstacles()
    {
        var map = new GridMap();
        map.SetObstacle(5, 5);
        map.SetObstacle(10, 10);
        map.SetObstacle(20, 15);

        map.ClearDynamicObstacles();

        Assert.Equal(CellType.Empty, map.GetCell(5, 5));
        Assert.Equal(CellType.Empty, map.GetCell(10, 10));
        Assert.Equal(CellType.Empty, map.GetCell(20, 15));
    }

    [Fact]
    public void ClearDynamicObstacles_ShouldNotRemoveStaticWalls()
    {
        var map = new GridMap();
        map.InitStaticWalls();

        // Set obstacle then clear
        map.SetObstacle(5, 5);
        map.ClearDynamicObstacles();

        // Obstacle gone, walls remain
        Assert.Equal(CellType.Empty, map.GetCell(5, 5));
        Assert.Equal(CellType.StaticWall, map.GetCell(0, 0));        // Perimeter
        Assert.Equal(CellType.StaticWall, map.GetCell(10, 5));       // Shelf 1
    }

    // =====================================================================
    // 7. WorldToGrid — Coordinate Conversion
    // =====================================================================

    [Theory]
    [InlineData(0, 0, 0, 0)]                    // Origin
    [InlineData(499, 499, 0, 0)]                 // Still cell (0,0) — near boundary
    [InlineData(500, 500, 1, 1)]                 // Jumps to cell (1,1)
    [InlineData(2300, 1200, 4, 2)]               // Mid-map cell
    [InlineData(19999, 9999, 39, 19)]            // Last valid cell
    public void WorldToGrid_ValidCoordinates_ShouldConvertCorrectly(
        double xMm, double yMm, int expectedX, int expectedY)
    {
        var map = new GridMap();
        var (gx, gy) = map.WorldToGrid(xMm, yMm);

        Assert.Equal(expectedX, gx);
        Assert.Equal(expectedY, gy);
    }

    [Theory]
    [InlineData(-500, 1000)]          // Negative X
    [InlineData(1000, -500)]          // Negative Y
    [InlineData(20000, 1000)]         // X >= 20000mm (cell 40 -> out of bounds)
    [InlineData(1000, 10000)]         // Y >= 10000mm (cell 20 -> out of bounds)
    [InlineData(99999, 99999)]        // Far beyond bounds
    public void WorldToGrid_OutOfBounds_ShouldReturnMinusOne(double xMm, double yMm)
    {
        var map = new GridMap();
        var (gx, gy) = map.WorldToGrid(xMm, yMm);

        Assert.Equal(-1, gx);
        Assert.Equal(-1, gy);
    }

    // =====================================================================
    // 8. ToArray — Serialization
    // =====================================================================

    [Fact]
    public void ToArray_ShouldMatchGridDimensions()
    {
        var map = new GridMap();
        var arr = map.ToArray();

        Assert.Equal(GridMap.Width, arr.GetLength(0));
        Assert.Equal(GridMap.Height, arr.GetLength(1));
    }

    [Fact]
    public void ToArray_ShouldReflectCurrentGridState()
    {
        var map = new GridMap();
        map.InitStaticWalls();
        map.SetObstacle(15, 10);

        var arr = map.ToArray();

        Assert.Equal((int)CellType.StaticWall, arr[0, 0]);          // Perimeter
        Assert.Equal((int)CellType.DynamicObstacle, arr[15, 10]);   // Obstacle
        Assert.Equal((int)CellType.Empty, arr[20, 10]);             // Empty interior
    }

    // =====================================================================
    // 9. Integration — Full Control Loop Lifecycle
    // =====================================================================

    [Fact]
    public void ControlLoopLifecycle_SetObstacle_Clear_ShouldResetToEmpty()
    {
        // Simulate: Vision AI detects obstacle -> set -> clear on next cycle
        var map = new GridMap();
        map.InitStaticWalls();

        // Cycle 1: Vision AI detects person at cell (15, 10)
        map.SetObstacle(15, 10);
        Assert.False(map.IsWalkable(15, 10));

        // Cycle 2: Clear previous, Vision AI no longer detects
        map.ClearDynamicObstacles();
        Assert.True(map.IsWalkable(15, 10));     // Cell is free again
        Assert.False(map.IsWalkable(0, 0));       // Wall unchanged
    }

    [Fact]
    public void ControlLoopLifecycle_ObstacleMovesPosition()
    {
        // Simulate: Person moves from cell (15,10) to (16,10) between cycles
        var map = new GridMap();
        map.InitStaticWalls();

        // Cycle 1
        map.SetObstacle(15, 10);
        Assert.False(map.IsWalkable(15, 10));
        Assert.True(map.IsWalkable(16, 10));

        // Cycle 2: Clear + set new position
        map.ClearDynamicObstacles();
        map.SetObstacle(16, 10);
        Assert.True(map.IsWalkable(15, 10));     // Old position free
        Assert.False(map.IsWalkable(16, 10));     // New position blocked
    }

    [Fact]
    public void FullPipeline_WorldToGrid_ThenSetObstacle_ThenCheckWalkable()
    {
        // Simulate: Vision AI detects obstacle at 7500mm, 5000mm
        var map = new GridMap();

        var (gx, gy) = map.WorldToGrid(7500, 5000);
        Assert.Equal(15, gx);   // 7500 / 500 = 15
        Assert.Equal(10, gy);   // 5000 / 500 = 10

        map.SetObstacle(gx, gy);
        Assert.False(map.IsWalkable(gx, gy));
        Assert.Equal(CellType.DynamicObstacle, map.GetCell(gx, gy));
    }
}
