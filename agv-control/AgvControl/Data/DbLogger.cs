// ==========================================================================
// Data/DbLogger.cs — PostgreSQL logging via Npgsql
// ==========================================================================
// Logs AGV paths and system events to PostgreSQL.
//
// Design:
// - S: Only responsible for DB write operations
// - D: AgvOrchestrator depends on IDbLogger, not this concrete class
// - Fire-and-forget: DB failure logs warning, never throws, never crashes the control loop
// - Connection pooling: new NpgsqlConnection() per call — Npgsql manages the pool
// - KISS: no ORM, plain parameterized SQL matching init.sql exactly
// ==========================================================================

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AgvControl.Data;

// ---------------------------------------------------------------------------
// Configuration — bound from appsettings.json "Database" section
// ---------------------------------------------------------------------------

/// <summary>Database connection settings. Bound from appsettings.json "Database" section.</summary>
public class DatabaseSettings
{
    /// <summary>
    /// Full Npgsql connection string.
    /// Example: "Host=localhost;Port=5432;Database=agv_control_db;Username=postgres;Password=1111"
    /// Override via environment variable: Database__ConnectionString
    /// </summary>
    public string ConnectionString { get; set; } =
        "Host=localhost;Port=5432;Database=agv_control_db;Username=postgres;Password=1111";
}

// ---------------------------------------------------------------------------
// PathRecord — data bag passed by AgvOrchestrator when a trip completes
// ---------------------------------------------------------------------------

/// <summary>
/// Captures one AGV trip for persistence in the <c>paths</c> table.
/// Matches database/init.sql schema exactly.
/// </summary>
public class PathRecord
{
    public double StartX { get; init; }
    public double StartY { get; init; }
    public double EndX   { get; init; }
    public double EndY   { get; init; }

    /// <summary>A* waypoints serialized as JSON. Format: [{x,y}, ...]</summary>
    public string? WaypointsJson       { get; init; }
    public double? TotalDistanceMm     { get; init; }
    public int?    PlannedDurationSec  { get; init; }
    public int?    ActualDurationSec   { get; init; }
    public int     ReplanningCount     { get; init; } = 0;

    /// <summary>planned | executing | completed | aborted</summary>
    public string  Status      { get; init; } = "completed";
    public string? AbortReason { get; init; }

    public DateTime? CompletedAt { get; init; }
}

// ---------------------------------------------------------------------------
// Interface
// ---------------------------------------------------------------------------

public interface IDbLogger
{
    /// <summary>
    /// Persist a completed AGV path to the <c>paths</c> table.
    /// Silently logs warning on DB failure — never throws.
    /// </summary>
    Task LogPathAsync(PathRecord record, CancellationToken ct = default);

    /// <summary>
    /// Persist a system event to the <c>system_logs</c> table.
    /// Mirrors Python SystemLogger.log_event() signature for consistency.
    /// Silently logs warning on DB failure — never throws.
    /// </summary>
    Task LogSystemEventAsync(
        string              level,
        string              component,
        string              eventType,
        string              message,
        object?             details            = null,
        int?                agvSpeedMms        = null,
        int?                batteryPercentage  = null,
        double?             positionX          = null,
        double?             positionY          = null,
        long?               pathId             = null,
        long?               detectionId        = null,
        CancellationToken   ct                 = default);

    /// <summary>
    /// Returns true if the DB is reachable. Used by HealthController.
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Implementation
// ---------------------------------------------------------------------------

/// <summary>
/// PostgreSQL logger using Npgsql.
/// Registered as Singleton — Npgsql handles connection pooling internally,
/// so creating <c>new NpgsqlConnection()</c> per call is the correct pattern.
/// </summary>
public class DbLogger : IDbLogger
{
    private readonly string          _connectionString;
    private readonly ILogger<DbLogger> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public DbLogger(IOptions<DatabaseSettings> settings, ILogger<DbLogger> logger)
    {
        _connectionString = settings.Value.ConnectionString;
        _logger           = logger;
    }

    // -----------------------------------------------------------------------
    // LogPathAsync
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task LogPathAsync(PathRecord record, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO paths (
                completed_at,
                start_x, start_y, end_x, end_y,
                waypoints, total_distance_mm,
                planned_duration_sec, actual_duration_sec,
                replanning_count, status, abort_reason
            )
            VALUES (
                @completedAt,
                @startX, @startY, @endX, @endY,
                @waypoints::jsonb, @totalDistanceMm,
                @plannedDurationSec, @actualDurationSec,
                @replanningCount, @status, @abortReason
            )
            RETURNING id;
            """;

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@completedAt",        (object?)record.CompletedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@startX",             record.StartX);
            cmd.Parameters.AddWithValue("@startY",             record.StartY);
            cmd.Parameters.AddWithValue("@endX",               record.EndX);
            cmd.Parameters.AddWithValue("@endY",               record.EndY);
            cmd.Parameters.AddWithValue("@waypoints",          (object?)record.WaypointsJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@totalDistanceMm",    (object?)record.TotalDistanceMm ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@plannedDurationSec", (object?)record.PlannedDurationSec ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@actualDurationSec",  (object?)record.ActualDurationSec ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@replanningCount",    record.ReplanningCount);
            cmd.Parameters.AddWithValue("@status",             record.Status);
            cmd.Parameters.AddWithValue("@abortReason",        (object?)record.AbortReason ?? DBNull.Value);

            var id = await cmd.ExecuteScalarAsync(ct);
            _logger.LogInformation("Path logged — id={PathId} status={Status}", id, record.Status);
        }
        catch (Exception ex)
        {
            // Fire-and-forget: DB failure must NOT crash the control loop
            _logger.LogWarning(ex, "LogPathAsync failed — DB write skipped");
        }
    }

    // -----------------------------------------------------------------------
    // LogSystemEventAsync
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task LogSystemEventAsync(
        string            level,
        string            component,
        string            eventType,
        string            message,
        object?           details           = null,
        int?              agvSpeedMms       = null,
        int?              batteryPercentage = null,
        double?           positionX         = null,
        double?           positionY         = null,
        long?             pathId            = null,
        long?             detectionId       = null,
        CancellationToken ct                = default)
    {
        const string sql = """
            INSERT INTO system_logs (
                level, component, event_type, message, details,
                agv_speed_mms, battery_percentage, position_x, position_y,
                path_id, detection_id
            )
            VALUES (
                @level, @component, @eventType, @message, @details::jsonb,
                @agvSpeedMms, @batteryPercentage, @positionX, @positionY,
                @pathId, @detectionId
            );
            """;

        // Serialize details object → JSON string (null stays null)
        string? detailsJson = details is null
            ? null
            : JsonSerializer.Serialize(details, _jsonOptions);

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@level",             level);
            cmd.Parameters.AddWithValue("@component",         component);
            cmd.Parameters.AddWithValue("@eventType",         eventType);
            cmd.Parameters.AddWithValue("@message",           message);
            cmd.Parameters.AddWithValue("@details",           (object?)detailsJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@agvSpeedMms",       (object?)agvSpeedMms ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@batteryPercentage", (object?)batteryPercentage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@positionX",         (object?)positionX ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@positionY",         (object?)positionY ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pathId",            (object?)pathId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@detectionId",       (object?)detectionId ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogDebug("System event logged: [{Level}] {Component} — {Message}", level, component, message);
        }
        catch (Exception ex)
        {
            // Fire-and-forget: DB failure must NOT crash the control loop
            _logger.LogWarning(ex, "LogSystemEventAsync failed — event skipped [{Level}] {Message}", level, message);
        }
    }

    // -----------------------------------------------------------------------
    // HealthCheckAsync
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = new NpgsqlCommand("SELECT 1;", conn);
            await cmd.ExecuteScalarAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DB health check failed");
            return false;
        }
    }
}
