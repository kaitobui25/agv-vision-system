// ==========================================================================
// AgvOrchestrator.cs — Main AGV control loop (BackgroundService)
// ==========================================================================
// Connects Vision AI → A* pathfinding → Modbus motor control → DB logging.
//
// Control loop runs every 100ms:
//   1. Read AGV state from Modbus
//   2. Poll Vision AI → update obstacle grid
//   3. Dispatch to state handler (Spinning / Moving / Idle)
//
// State machine:
//   Idle     — no target, no motor commands sent
//   Spinning — rotating in place toward next waypoint heading
//   Moving   — driving straight toward next waypoint
//
// Threading:
//   BackgroundService runs on a background thread.
//   StartTrip / StopTrip / EmergencyStop are called from HTTP thread.
//   All shared fields are protected by _lock.
//
// Design: SOLID, KISS, YAGNI — no PID, no obstacle inflation, no retry loop.
// ==========================================================================

using System.Drawing;
using System.Text.Json;
using AgvControl.Data;
using AgvControl.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgvControl.Services;

// ---------------------------------------------------------------------------
// Internal state machine enum
// ---------------------------------------------------------------------------
public enum OrchestratorState
{
    Idle,
    Spinning,
    Moving,
}

// ---------------------------------------------------------------------------
// AgvOrchestrator
// ---------------------------------------------------------------------------
public class AgvOrchestrator : BackgroundService
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------
    private const int    SpinRpm             = 200;   // RPM for in-place rotation
    private const int    MoveRpm             = 300;   // RPM for straight movement
    private const double HeadingThresholdDeg = 10.0;  // degrees — "aligned enough"
    private const double WaypointReachedMm   = 250.0; // Euclidean arrival threshold
    private const int    ReplanCooldownMs    = 500;   // min ms between replans
    private const int    CameraOffsetMm      = 300;   // camera to AGV center offset

    // -----------------------------------------------------------------------
    // Dependencies
    // -----------------------------------------------------------------------
    private readonly IVisionClient            _vision;
    private readonly IModbusClient            _modbus;
    private readonly IPathPlanner             _planner;
    private readonly IDbLogger                _db;
    private readonly ILogger<AgvOrchestrator> _logger;

    // -----------------------------------------------------------------------
    // Thread safety
    // -----------------------------------------------------------------------
    private readonly object _lock = new();

    // -----------------------------------------------------------------------
    // State machine fields  (always access inside lock)
    // -----------------------------------------------------------------------
    private OrchestratorState _state     = OrchestratorState.Idle;
    private Point?            _target;                      // grid coords, null = no trip
    private List<Point>       _waypoints = [];              // [0] = next waypoint

    // -----------------------------------------------------------------------
    // Trip tracking  (always access inside lock)
    // -----------------------------------------------------------------------
    private DateTime? _tripStartTime;
    private int       _replanCount;
    private DateTime  _lastReplanTime = DateTime.MinValue;

    // -----------------------------------------------------------------------
    // Shared state — read by Controllers via copy  (always access inside lock)
    // -----------------------------------------------------------------------
    private AgvState _currentState = new();
    private GridMap  _currentMap   = new();

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public AgvOrchestrator(
        IVisionClient            vision,
        IModbusClient            modbus,
        IPathPlanner             planner,
        IDbLogger                db,
        ILogger<AgvOrchestrator> logger)
    {
        _vision  = vision;
        _modbus  = modbus;
        _planner = planner;
        _db      = db;
        _logger  = logger;
    }

    // =======================================================================
    // Public API — called by Controllers (HTTP thread)
    // =======================================================================

    /// <summary>
    /// Start a trip to the given target (mm coordinates).
    /// Converts target mm → grid cell, runs initial A*, transitions to Spinning.
    /// </summary>
    public void StartTrip(double targetX, double targetY)
    {
        Point targetCell, startCell;
        GridMap mapSnapshot;

        lock (_lock)
        {
            var (gx, gy) = _currentMap.WorldToGrid(targetX, targetY);
            if (gx == -1)
            {
                _logger.LogWarning("StartTrip: target ({X},{Y}) out of bounds", targetX, targetY);
                return;
            }

            targetCell = new Point(gx, gy);
            var (sx, sy)   = _currentMap.WorldToGrid(_currentState.PositionX, _currentState.PositionY);
            startCell  = new Point(sx < 0 ? 0 : sx, sy < 0 ? 0 : sy);

            mapSnapshot = _currentMap.Clone();
        }

        List<Point> path;
        try
        {
            // Pathfinding runs OUTSIDE the lock to prevent blocking TickAsync and HTTP threads
            path = _planner.FindPath(mapSnapshot, startCell, targetCell);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("StartTrip: no path to target — {Msg}", ex.Message);
            return;
        }

        lock (_lock)
        {
            // If another trip started while we were planning, it gets overwritten by this one.
            _target        = targetCell;
            _waypoints     = path;
            _state         = OrchestratorState.Spinning;
            _tripStartTime = DateTime.UtcNow;
            _replanCount   = 0;

            _logger.LogInformation(
                "Trip started → target grid ({Gx},{Gy}), {Count} waypoints",
                targetCell.X, targetCell.Y, path.Count);

            // Fire-and-forget log — DB failure must not block
            _ = OnTripStartedAsync(startCell, targetCell, path);
        }
    }

    /// <summary>Gradual stop — send Stop command, clear trip.</summary>
    public void StopTrip()
    {
        lock (_lock)
        {
            _state     = OrchestratorState.Idle;
            _target    = null;
            _waypoints = [];
        }
        _ = SendMotorCommandAsync(0, 0, CommandCode.Stop);
        _logger.LogInformation("Trip stopped by request");
    }

    /// <summary>Emergency stop — immediate motor cut, clear trip.</summary>
    public void EmergencyStop()
    {
        lock (_lock)
        {
            _state     = OrchestratorState.Idle;
            _target    = null;
            _waypoints = [];
        }
        _ = SendMotorCommandAsync(0, 0, CommandCode.EmergencyStop);
        _logger.LogWarning("EMERGENCY STOP triggered");
    }

    /// <summary>Returns a snapshot copy of current AGV state (thread-safe).</summary>
    public AgvState GetCurrentState()
    {
        lock (_lock)
        {
            // Shallow copy — all value-type properties
            return new AgvState
            {
                PositionX        = _currentState.PositionX,
                PositionY        = _currentState.PositionY,
                HeadingDegrees   = _currentState.HeadingDegrees,
                ActualLeftSpeed  = _currentState.ActualLeftSpeed,
                ActualRightSpeed = _currentState.ActualRightSpeed,
                BatteryLevel     = _currentState.BatteryLevel,
                Status           = _currentState.Status,
                Error            = _currentState.Error,
                LastUpdated      = _currentState.LastUpdated,
            };
        }
    }

    /// <summary>Returns a deep copy of the current grid map (thread-safe).</summary>
    public GridMap GetCurrentMap()
    {
        lock (_lock)
        {
            return _currentMap.Clone();
        }
    }

    // =======================================================================
    // BackgroundService entry point
    // =======================================================================

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("AgvOrchestrator starting...");

        // Init static warehouse walls once
        lock (_lock) { _currentMap.InitStaticWalls(); }

        // Connect to hardware sim — retry until success or cancellation
        await ConnectWithRetryAsync(ct);

        _logger.LogInformation("AgvOrchestrator running");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TickAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in tick — continuing");
            }

            await Task.Delay(100, ct);
        }

        _logger.LogInformation("AgvOrchestrator stopped");
    }

    // =======================================================================
    // Tick — one 100ms iteration
    // =======================================================================

    internal async Task TickAsync(CancellationToken ct)
    {
        // 1. Read AGV state from Modbus
        await UpdateAgvStateAsync();

        // 2. Poll Vision AI → update obstacle grid
        await UpdateObstaclesAsync();

        // 3. Dispatch to state handler
        OrchestratorState currentState;
        lock (_lock) { currentState = _state; }

        switch (currentState)
        {
            case OrchestratorState.Idle:
                break; // nothing to do

            case OrchestratorState.Spinning:
                await HandleSpinningAsync();
                break;

            case OrchestratorState.Moving:
                await HandleMovingAsync();
                break;
        }
    }

    // =======================================================================
    // Sensor layer
    // =======================================================================

    /// <summary>Read AGV state from Modbus → update _currentState.</summary>
    private async Task UpdateAgvStateAsync()
    {
        try
        {
            var state = await _modbus.ReadStatusAsync();
            lock (_lock) { _currentState = state; }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Modbus read failed: {Msg}", ex.Message);
            // Keep last known state — do not crash the loop
        }
    }

    /// <summary>Poll Vision AI → convert detections to grid obstacles.</summary>
    private async Task UpdateObstaclesAsync()
    {
        var response = await _vision.GetLatestDetectionsAsync();
        if (response is null) return; // Vision AI unreachable — keep existing obstacles

        lock (_lock)
        {
            _currentMap.ClearDynamicObstacles();

            double headingRad = _currentState.HeadingRadians;

            foreach (var detection in response.Detections)
            {
                if (detection.DistanceMeters is null) continue;

                double distMm      = detection.DistanceMeters.Value * 1000.0 + CameraOffsetMm;
                double obstacleXMm = _currentState.PositionX + distMm * Math.Cos(headingRad);
                double obstacleYMm = _currentState.PositionY + distMm * Math.Sin(headingRad);

                var (gx, gy) = _currentMap.WorldToGrid(obstacleXMm, obstacleYMm);
                if (gx != -1)
                    _currentMap.SetObstacle(gx, gy);
            }
        }
    }

    // =======================================================================
    // State handlers
    // =======================================================================

    /// <summary>
    /// Spinning: rotate in place toward waypoint[0].
    /// Transitions to Moving when heading is within threshold.
    /// IMPORTANT: does NOT send MOVE in the same tick as aligning — avoids jitter.
    /// </summary>
    private async Task HandleSpinningAsync()
    {
        Point waypoint;
        AgvState agvState;

        lock (_lock)
        {
            if (_waypoints.Count == 0)
            {
                _state = OrchestratorState.Idle;
                return;
            }
            waypoint = _waypoints[0];
            agvState = _currentState;
        }

        double targetAngle = ComputeTargetAngle(waypoint, agvState);
        double angleDiff   = NormalizeAngle(targetAngle - agvState.HeadingDegrees);

        if (Math.Abs(angleDiff) < HeadingThresholdDeg)
        {
            // Aligned — stop spinning, transition to Moving (MOVE sent next tick)
            await SendMotorCommandAsync(0, 0, CommandCode.Stop);
            lock (_lock) { _state = OrchestratorState.Moving; }
            _logger.LogDebug("Aligned to waypoint ({X},{Y}), switching to Moving", waypoint.X, waypoint.Y);
        }
        else if (angleDiff > 0)
        {
            // Turn left: left backward, right forward
            await SendMotorCommandAsync(-SpinRpm, SpinRpm, CommandCode.Move);
        }
        else
        {
            // Turn right: left forward, right backward
            await SendMotorCommandAsync(SpinRpm, -SpinRpm, CommandCode.Move);
        }
    }

    /// <summary>
    /// Moving: drive straight toward waypoint[0].
    /// Handles arrival, obstacle replan, and normal forward drive.
    /// </summary>
    private async Task HandleMovingAsync()
    {
        Point waypoint;
        AgvState agvState;

        lock (_lock)
        {
            if (_waypoints.Count == 0)
            {
                _state = OrchestratorState.Idle;
                return;
            }
            waypoint = _waypoints[0];
            agvState = _currentState;
        }

        double dist = DistanceToWaypoint(waypoint, agvState);

        // ── Arrived ──────────────────────────────────────────────────────────
        if (dist < WaypointReachedMm)
        {
            AdvanceWaypoint();

            int remaining;
            lock (_lock) { remaining = _waypoints.Count; }

            if (remaining == 0)
            {
                // Trip complete
                await SendMotorCommandAsync(0, 0, CommandCode.Stop);
                await OnTripCompletedAsync();
                lock (_lock)
                {
                    _state  = OrchestratorState.Idle;
                    _target = null;
                }
            }
            else
            {
                // More waypoints — re-align heading
                lock (_lock) { _state = OrchestratorState.Spinning; }
            }
            return;
        }

        // ── Obstacle check ───────────────────────────────────────────────────
        if (IsNextWaypointBlocked())
        {
            bool cooldownElapsed;
            lock (_lock)
            {
                cooldownElapsed = (DateTime.UtcNow - _lastReplanTime).TotalMilliseconds > ReplanCooldownMs;
            }

            if (cooldownElapsed)
            {
                lock (_lock) { _lastReplanTime = DateTime.UtcNow; }

                bool success = await TryReplanAsync();
                lock (_lock)
                {
                    _state = OrchestratorState.Spinning; // always re-align after replan
                }

                if (!success)
                {
                    await SendMotorCommandAsync(0, 0, CommandCode.Stop);
                    // target intentionally kept — retry next tick
                }
            }
            // else: still in cooldown — keep driving (A* uses DynamicObstacle cost, not wall)
            return;
        }

        // ── Normal forward drive ─────────────────────────────────────────────
        await SendMotorCommandAsync(MoveRpm, MoveRpm, CommandCode.Move);
    }

    // =======================================================================
    // Steering helpers
    // =======================================================================

    /// <summary>
    /// Compute angle from AGV position to the CENTER of the waypoint cell (degrees).
    /// Uses atan2 — no snapping to cardinal directions.
    /// </summary>
    private static double ComputeTargetAngle(Point waypoint, AgvState agvState)
    {
        double wpCenterX = waypoint.X * GridMap.CellSizeMm + GridMap.CellSizeMm / 2.0;
        double wpCenterY = waypoint.Y * GridMap.CellSizeMm + GridMap.CellSizeMm / 2.0;
        double dx        = wpCenterX - agvState.PositionX;
        double dy        = wpCenterY - agvState.PositionY;
        return Math.Atan2(dy, dx) * 180.0 / Math.PI; // degrees
    }

    /// <summary>Normalize angle to -180..180 range.</summary>
    private static double NormalizeAngle(double deg)
    {
        while (deg >  180.0) deg -= 360.0;
        while (deg < -180.0) deg += 360.0;
        return deg;
    }

    /// <summary>Euclidean distance (mm) from AGV position to center of waypoint cell.</summary>
    private static double DistanceToWaypoint(Point waypoint, AgvState agvState)
    {
        double wpCenterX = waypoint.X * GridMap.CellSizeMm + GridMap.CellSizeMm / 2.0;
        double wpCenterY = waypoint.Y * GridMap.CellSizeMm + GridMap.CellSizeMm / 2.0;
        double dx        = wpCenterX - agvState.PositionX;
        double dy        = wpCenterY - agvState.PositionY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // =======================================================================
    // Path helpers
    // =======================================================================

    /// <summary>True if the next waypoint cell is blocked (DynamicObstacle or StaticWall).</summary>
    private bool IsNextWaypointBlocked()
    {
        lock (_lock)
        {
            if (_waypoints.Count == 0) return false;
            var wp   = _waypoints[0];
            var cell = _currentMap.GetCell(wp.X, wp.Y);
            return cell != CellType.Empty;
        }
    }

    /// <summary>
    /// Attempt to replan path from current AGV position to _target.
    /// Replaces _waypoints on success.
    /// Returns false (and logs) if A* throws — target is kept for retry.
    /// </summary>
    private async Task<bool> TryReplanAsync()
    {
        Point target;
        Point startCell;
        GridMap mapSnapshot;

        lock (_lock)
        {
            if (_target is null) return false;
            target = _target.Value;

            var (sx, sy) = _currentMap.WorldToGrid(_currentState.PositionX, _currentState.PositionY);
            startCell    = new Point(sx < 0 ? 0 : sx, sy < 0 ? 0 : sy);
            mapSnapshot  = _currentMap.Clone(); // plan outside lock
        }

        try
        {
            var newPath = _planner.FindPath(mapSnapshot, startCell, target);

            lock (_lock)
            {
                _waypoints = newPath; // discard old waypoints completely
                _replanCount++;
            }

            _logger.LogInformation("Replanned: {Count} waypoints (replan #{N})", newPath.Count, _replanCount);

            // Fire-and-forget
            _ = _db.LogSystemEventAsync(
                    level:     "info",
                    component: "AgvOrchestrator",
                    eventType: "replan",
                    message:   $"Replanned due to blocked waypoint (#{_replanCount})");

            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Replan failed — no path: {Msg}", ex.Message);
            await OnTripErrorAsync($"No path found during replan: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Pop waypoint[0] from the list.
    /// Guard: does nothing if list is already empty.
    /// </summary>
    private void AdvanceWaypoint()
    {
        lock (_lock)
        {
            if (_waypoints.Count > 0)
                _waypoints.RemoveAt(0);
        }
    }

    // =======================================================================
    // Motor helper
    // =======================================================================

    /// <summary>
    /// Wrapper for IModbusClient.WriteMotorCommandAsync.
    /// Catches and logs exceptions — motor failure must not crash the loop.
    /// </summary>
    private async Task SendMotorCommandAsync(int leftRpm, int rightRpm, CommandCode cmd)
    {
        try
        {
            await _modbus.WriteMotorCommandAsync((short)leftRpm, (short)rightRpm, cmd);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Motor command failed ({Left},{Right},{Cmd}): {Msg}",
                leftRpm, rightRpm, cmd, ex.Message);
        }
    }

    // =======================================================================
    // Trip lifecycle — logging (fire-and-forget, never throw)
    // =======================================================================

    private async Task OnTripStartedAsync(Point start, Point target, List<Point> waypoints)
    {
        try
        {
            var waypointsJson = JsonSerializer.Serialize(
                waypoints.Select(p => new { x = p.X, y = p.Y }));

            var record = new PathRecord
            {
                StartX        = start.X  * GridMap.CellSizeMm,
                StartY        = start.Y  * GridMap.CellSizeMm,
                EndX          = target.X * GridMap.CellSizeMm,
                EndY          = target.Y * GridMap.CellSizeMm,
                WaypointsJson = waypointsJson,
                Status        = "executing",
                CompletedAt   = null,
            };

            // Note: LogPathAsync returns void (no id returned per current interface)
            await _db.LogPathAsync(record);

            _logger.LogDebug("Trip started log written");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("OnTripStartedAsync log failed: {Msg}", ex.Message);
        }
    }

    private async Task OnTripCompletedAsync()
    {
        int replanCount;
        DateTime? startTime;
        Point?    target;

        lock (_lock)
        {
            replanCount = _replanCount;
            startTime   = _tripStartTime;
            target      = _target;
        }

        int? durationSec = startTime.HasValue
            ? (int)(DateTime.UtcNow - startTime.Value).TotalSeconds
            : null;

        try
        {
            var record = new PathRecord
            {
                StartX             = 0, // start position was logged at OnTripStarted
                StartY             = 0,
                EndX               = (target?.X ?? 0) * GridMap.CellSizeMm,
                EndY               = (target?.Y ?? 0) * GridMap.CellSizeMm,
                Status             = "completed",
                ReplanningCount    = replanCount,
                ActualDurationSec  = durationSec,
                CompletedAt        = DateTime.UtcNow,
            };

            await _db.LogPathAsync(record);
            _logger.LogInformation("Trip completed in {Sec}s with {N} replans", durationSec, replanCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("OnTripCompletedAsync log failed: {Msg}", ex.Message);
        }
    }

    private async Task OnTripErrorAsync(string message)
    {
        AgvState agvState;
        lock (_lock) { agvState = _currentState; }

        try
        {
            _ = _db.LogSystemEventAsync(
                    level:             "error",
                    component:         "AgvOrchestrator",
                    eventType:         "no_path",
                    message:           message,
                    batteryPercentage: agvState.BatteryLevel,
                    positionX:         agvState.PositionX,
                    positionY:         agvState.PositionY);

            _logger.LogError("Trip error: {Msg}", message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("OnTripErrorAsync log failed: {Msg}", ex.Message);
        }

        await Task.CompletedTask;
    }

    // =======================================================================
    // Modbus connect with retry
    // =======================================================================

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        int[] delaysMs = [1000, 2000, 5000];
        int   attempt  = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _modbus.ConnectAsync();
                _logger.LogInformation("Modbus connected");
                return;
            }
            catch (Exception ex)
            {
                int delay = delaysMs[Math.Min(attempt, delaysMs.Length - 1)];
                _logger.LogWarning("Modbus connect attempt {N} failed: {Msg}. Retry in {D}ms",
                    attempt + 1, ex.Message, delay);
                await Task.Delay(delay, ct);
                attempt++;
            }
        }
    }
}
