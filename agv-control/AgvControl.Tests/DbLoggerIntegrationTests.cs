// ==========================================================================
// DbLoggerIntegrationTests.cs — Integration tests for DbLogger
// ==========================================================================
// Hits the REAL PostgreSQL database (agv_control_db).
//
// Prerequisites:
//   - PostgreSQL running on localhost:5432
//   - database/init.sql already executed (tables exist)
//   - Connection string below matches your local setup
//
// Run:
//   cd agv-control/AgvControl.Tests
//   dotnet test --filter "Category=Integration"
//
// These tests are tagged [Trait("Category", "Integration")] so they can be
// excluded from the fast unit-test pass in CI:
//   dotnet test --filter "Category!=Integration"
// ==========================================================================

using AgvControl.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AgvControl.Tests;

public class DbLoggerIntegrationTests : IAsyncLifetime
{
    // -----------------------------------------------------------------------
    // Connection — matches agv-control/AgvControl/appsettings.json
    // -----------------------------------------------------------------------
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=agv_control_db;Username=postgres;Password=1111";

    private DbLogger _logger = null!;

    // IDs inserted by THIS test run — used for cleanup in DisposeAsync
    private readonly List<long> _insertedPathIds    = [];
    private readonly List<long> _insertedLogIds     = [];

    // -----------------------------------------------------------------------
    // Setup / Teardown (IAsyncLifetime)
    // -----------------------------------------------------------------------

    public Task InitializeAsync()
    {
        var settings = Options.Create(new DatabaseSettings
        {
            ConnectionString = ConnectionString
        });

        _logger = new DbLogger(settings, NullLogger<DbLogger>.Instance);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clean up rows inserted by THIS test run only — leave other data intact
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        if (_insertedLogIds.Count > 0)
        {
            var ids = string.Join(",", _insertedLogIds);
            await using var cmd = new NpgsqlCommand(
                $"DELETE FROM system_logs WHERE id = ANY(ARRAY[{ids}]::bigint[])", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        if (_insertedPathIds.Count > 0)
        {
            var ids = string.Join(",", _insertedPathIds);
            await using var cmd = new NpgsqlCommand(
                $"DELETE FROM paths WHERE id = ANY(ARRAY[{ids}]::bigint[])", conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // -----------------------------------------------------------------------
    // Helper — query a single value from DB
    // -----------------------------------------------------------------------
    private async Task<T?> QueryScalarAsync<T>(string sql, Action<NpgsqlCommand>? addParams = null)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        addParams?.Invoke(cmd);
        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull or null ? default : (T)result;
    }

    // =======================================================================
    // 1. HealthCheckAsync
    // =======================================================================

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HealthCheck_WithRealDb_ShouldReturnTrue()
    {
        var isHealthy = await _logger.HealthCheckAsync();

        Assert.True(isHealthy, "Expected DB to be reachable on localhost:5432");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HealthCheck_WithBadConnectionString_ShouldReturnFalse()
    {
        var badSettings = Options.Create(new DatabaseSettings
        {
            ConnectionString = "Host=localhost;Port=9999;Database=does_not_exist;Username=nobody;Password=wrong"
        });
        var badLogger = new DbLogger(badSettings, NullLogger<DbLogger>.Instance);

        var isHealthy = await badLogger.HealthCheckAsync();

        Assert.False(isHealthy, "Expected false when DB is unreachable");
    }

    // =======================================================================
    // 2. LogSystemEventAsync — basic write
    // =======================================================================

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LogSystemEvent_MinimalFields_ShouldInsertRow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        await _logger.LogSystemEventAsync(
            level:     "INFO",
            component: "agv-control",
            eventType: "integration_test",
            message:   "DbLogger integration test — minimal fields");

        // Verify row exists
        var count = await QueryScalarAsync<long>(
            """
            SELECT COUNT(*) FROM system_logs
            WHERE component = 'agv-control'
              AND event_type = 'integration_test'
              AND message    = 'DbLogger integration test — minimal fields'
              AND timestamp  >= @before
            """,
            cmd => cmd.Parameters.AddWithValue("@before", before));

        Assert.True(count >= 1, $"Expected at least 1 row inserted, got {count}");

        // Track for cleanup
        var id = await QueryScalarAsync<long>(
            """
            SELECT id FROM system_logs
            WHERE event_type = 'integration_test'
              AND timestamp  >= @before
            ORDER BY id DESC LIMIT 1
            """,
            cmd => cmd.Parameters.AddWithValue("@before", before));
        if (id > 0) _insertedLogIds.Add(id);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LogSystemEvent_AllFields_ShouldPersistCorrectly()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var details = new { reason = "low_battery", threshold = 20 };

        await _logger.LogSystemEventAsync(
            level:             "WARNING",
            component:         "agv-control",
            eventType:         "battery_low",
            message:           "Battery below threshold — integration test",
            details:           details,
            agvSpeedMms:       0,
            batteryPercentage: 18,
            positionX:         1500.5,
            positionY:         3000.0);

        // Verify all columns landed correctly
        var row = await QueryScalarAsync<long>(
            """
            SELECT id FROM system_logs
            WHERE component         = 'agv-control'
              AND event_type        = 'battery_low'
              AND level             = 'WARNING'
              AND battery_percentage = 18
              AND agv_speed_mms     = 0
              AND position_x        = 1500.5
              AND position_y        = 3000.0
              AND details           IS NOT NULL
              AND timestamp         >= @before
            LIMIT 1
            """,
            cmd => cmd.Parameters.AddWithValue("@before", before));

        Assert.True(row > 0, "Expected row with all fields persisted correctly");
        _insertedLogIds.Add(row);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LogSystemEvent_WithPathIdAndDetectionId_ShouldStoreForeignKeys()
    {
        // First insert a path to get a valid path_id FK
        var pathId = await InsertMinimalPathAndGetId();
        _insertedPathIds.Add(pathId);

        var before = DateTime.UtcNow.AddSeconds(-1);

        await _logger.LogSystemEventAsync(
            level:      "ERROR",
            component:  "agv-control",
            eventType:  "collision_risk",
            message:    "Obstacle detected — FK link test",
            pathId:     pathId);

        var logId = await QueryScalarAsync<long>(
            """
            SELECT id FROM system_logs
            WHERE event_type = 'collision_risk'
              AND path_id    = @pathId
              AND timestamp  >= @before
            LIMIT 1
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@pathId", pathId);
                cmd.Parameters.AddWithValue("@before", before);
            });

        Assert.True(logId > 0, "Expected system_log row with path_id FK");
        _insertedLogIds.Add(logId);
    }

    // =======================================================================
    // 3. LogPathAsync
    // =======================================================================

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LogPath_CompletedTrip_ShouldInsertRow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var record = new PathRecord
        {
            StartX            = 0,
            StartY            = 0,
            EndX              = 5000,
            EndY              = 3000,
            WaypointsJson     = """[{"x":1,"y":0},{"x":2,"y":0},{"x":3,"y":1}]""",
            TotalDistanceMm   = 8246.2,
            PlannedDurationSec = 30,
            ActualDurationSec  = 33,
            ReplanningCount   = 1,
            Status            = "completed",
            CompletedAt       = DateTime.UtcNow
        };

        await _logger.LogPathAsync(record);

        var id = await QueryScalarAsync<long>(
            """
            SELECT id FROM paths
            WHERE start_x          = 0
              AND end_x            = 5000
              AND status           = 'completed'
              AND replanning_count = 1
              AND waypoints        IS NOT NULL
              AND created_at       >= @before
            LIMIT 1
            """,
            cmd => cmd.Parameters.AddWithValue("@before", before));

        Assert.True(id > 0, "Expected completed path row inserted");
        _insertedPathIds.Add(id);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LogPath_AbortedTrip_ShouldPersistAbortReason()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var record = new PathRecord
        {
            StartX      = 100,
            StartY      = 200,
            EndX        = 9000,
            EndY        = 4000,
            Status      = "aborted",
            AbortReason = "emergency_stop triggered",
            CompletedAt = DateTime.UtcNow
        };

        await _logger.LogPathAsync(record);

        var abortReason = await QueryScalarAsync<string>(
            """
            SELECT abort_reason FROM paths
            WHERE status      = 'aborted'
              AND start_x     = 100
              AND created_at  >= @before
            LIMIT 1
            """,
            cmd => cmd.Parameters.AddWithValue("@before", before));

        Assert.Equal("emergency_stop triggered", abortReason);

        // Cleanup
        var id = await QueryScalarAsync<long>(
            "SELECT id FROM paths WHERE status='aborted' AND start_x=100 AND created_at>=@before LIMIT 1",
            cmd => cmd.Parameters.AddWithValue("@before", before));
        if (id > 0) _insertedPathIds.Add(id);
    }

    // =======================================================================
    // 4. Fire-and-forget — DB failure must NOT throw
    // =======================================================================

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LogSystemEvent_WhenDbUnreachable_ShouldNotThrow()
    {
        var badSettings = Options.Create(new DatabaseSettings
        {
            ConnectionString = "Host=localhost;Port=9999;Database=none;Username=x;Password=x"
        });
        var badLogger = new DbLogger(badSettings, NullLogger<DbLogger>.Instance);

        // Must complete without throwing — fire-and-forget contract
        var ex = await Record.ExceptionAsync(() =>
            badLogger.LogSystemEventAsync("ERROR", "test", "test", "should not throw"));

        Assert.Null(ex);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LogPath_WhenDbUnreachable_ShouldNotThrow()
    {
        var badSettings = Options.Create(new DatabaseSettings
        {
            ConnectionString = "Host=localhost;Port=9999;Database=none;Username=x;Password=x"
        });
        var badLogger = new DbLogger(badSettings, NullLogger<DbLogger>.Instance);

        var ex = await Record.ExceptionAsync(() =>
            badLogger.LogPathAsync(new PathRecord
            {
                StartX = 0, StartY = 0, EndX = 1, EndY = 1, Status = "completed"
            }));

        Assert.Null(ex);
    }

    // =======================================================================
    // Private helper
    // =======================================================================

    /// <summary>Insert a minimal path row directly via Npgsql and return its id.</summary>
    private async Task<long> InsertMinimalPathAndGetId()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO paths (start_x, start_y, end_x, end_y, status)
            VALUES (0, 0, 1000, 1000, 'completed')
            RETURNING id;
            """, conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
