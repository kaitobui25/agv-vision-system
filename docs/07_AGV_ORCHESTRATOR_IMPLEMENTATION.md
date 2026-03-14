# Implementation Plan — `Services/AgvOrchestrator.cs`

> File này đủ để bất kỳ developer hoặc AI agent nào đọc vào và triển khai
> `AgvOrchestrator.cs` mà không cần hỏi thêm.

---

## 1. Bối cảnh (Context)

`AgvOrchestrator` là **"não"** của hệ thống AGV — BackgroundService chạy vòng lặp
100ms, kết nối toàn bộ các service đã có:

```
Vision AI (Python)
      │ GET /detect/latest
      ▼
AgvOrchestrator  ──►  PathPlanner (A*)  ──►  ModbusClient  ──►  C++ hardware-sim
      │
      ▼
   DbLogger (PostgreSQL)
```

### Dependencies đã hoàn thành (Steps 1–7)

| Interface | Class | Vai trò |
|---|---|---|
| `IVisionClient` | `VisionClient` | Trả `VisionResponse?`, null nếu lỗi |
| `IModbusClient` | `ModbusClient` | `ReadStatusAsync()`, `WriteMotorCommandAsync()` |
| `IPathPlanner` | `PathPlanner` | `FindPath(map, start, goal)` → `List<Point>` hoặc throw |
| `IDbLogger` | `DbLogger` | `LogPathAsync()`, `LogSystemEventAsync()` |
| `GridMap` | — | 40×20 grid, `SetObstacle`, `ClearDynamicObstacles`, `WorldToGrid` |
| `AgvState` | — | position (mm), heading (degrees+radians), speed, battery, status |

### Vị trí file

```
agv-control/AgvControl/Services/AgvOrchestrator.cs
```

---

## 2. Các quyết định thiết kế (Decision Log)

| # | Quyết định | Alternatives đã loại | Lý do |
|---|---|---|---|
| 1 | **Two-phase steering**: spin tại chỗ → MOVE thẳng | Proportional, fixed speed | Chính xác trên grid 4 hướng, dễ debug |
| 2 | **Heading aligned** khi lệch < 10° | 5°, snap 90° | Không rung lắc, không phụ thuộc grid direction |
| 3 | **Waypoint reached** khi Euclidean < 250mm | Grid cell match, trust sim | Robust với simulation drift |
| 4 | **Replan chỉ khi waypoint[0] bị block** | Replan mỗi tick, STOP chờ | KISS, tránh A* spam |
| 5 | **Replan cooldown 500ms** | No cooldown, 1s | Cân bằng responsiveness vs CPU |
| 6 | **A* fail** → STOP + log + giữ target + `_state = Spinning` | Clear target, EmergencyStop | Trip vẫn active, obstacle có thể di chuyển |
| 7 | **`Completed` là inline event**, không phải state | `Completed` state | Tránh tick lơ lửng, state machine sạch |
| 8 | **Replan success/fail** → `_state = Spinning` | Giữ Moving | Heading mới có thể khác hoàn toàn |
| 9 | **Arrived** → `_state = Spinning` nếu còn waypoint | Tiếp tục Moving | Waypoint kế có thể rẽ hướng |
| 10 | **`lock` toàn bộ shared state** | `volatile` | Thread safety cho HTTP + background thread |
| 11 | **Controller đọc copy** của state/map | Direct reference | Tránh race condition khi serialize |
| 12 | **Không gửi MOVE ngay tick Spinning aligned** | Gửi ngay | Tránh spin→move jitter |
| 13 | **Log fire-and-forget** | Await log | DB failure không block control loop |

---

## 3. State Machine

### States

```
Idle       — không có target, không gửi lệnh motor
Spinning   — đang quay tại chỗ về hướng waypoint[0]
Moving     — đang tiến thẳng đến waypoint[0]
```

> ⚠️ `Completed` **không phải state** — là inline event xử lý ngay trong tick `Moving`.

### Transitions

```
Idle
 ├── StartTrip() được gọi → Spinning

Spinning
 ├── |angleDiff| < 10° → gửi STOP spin, _state = Moving (tick này kết thúc, KHÔNG gửi MOVE ngay)
 └── else → gửi spin command (200 RPM)

Moving
 ├── distance < 250mm (arrived):
 │     ├── _waypoints.RemoveAt(0)  [guard: chỉ nếu Count > 0]
 │     ├── Count == 0 → OnTripCompletedAsync(), _state = Idle
 │     └── Count > 0  → _state = Spinning
 ├── waypoint[0] bị block + cooldown > 500ms:
 │     ├── TryReplanAsync() success → _waypoints = newPath, _state = Spinning
 │     └── TryReplanAsync() fail   → STOP + OnTripErrorAsync(), _state = Spinning, giữ target
 └── else → gửi MOVE (300 RPM cả hai bánh)

Bất kỳ state nào:
 └── EmergencyStop() → gửi EmergencyStop command, _state = Idle, _target = null
```

---

## 4. Fields nội bộ

```csharp
// Dependencies
private readonly IVisionClient  _vision;
private readonly IModbusClient  _modbus;
private readonly IPathPlanner   _planner;
private readonly IDbLogger      _db;
private readonly ILogger<AgvOrchestrator> _logger;

// Thread safety
private readonly object _lock = new();

// State machine
private OrchestratorState _state = OrchestratorState.Idle;
private Point?            _target;           // grid coords, null = no trip
private List<Point>       _waypoints = [];   // index 0 = next waypoint

// Trip tracking
private DateTime? _tripStartTime;
private long?     _pathId;                   // từ LogPathAsync khi started
private DateTime  _lastReplanTime = DateTime.MinValue;

// Shared state — read by Controllers via copy
private AgvState  _currentState = new();
private GridMap   _currentMap   = new();
```

---

## 5. Skeleton đầy đủ

```csharp
public class AgvOrchestrator : BackgroundService
{
    // ─── Fields (như mục 4) ───────────────────────────────

    // ─── Constructor (DI) ────────────────────────────────
    public AgvOrchestrator(
        IVisionClient vision,
        IModbusClient modbus,
        IPathPlanner planner,
        IDbLogger db,
        ILogger<AgvOrchestrator> logger)

    // ─── Public API (called by Controllers) ──────────────
    public void StartTrip(double targetX, double targetY)
    public void StopTrip()
    public void EmergencyStop()
    public AgvState GetCurrentState()   // trả shallow copy (lock)
    public GridMap  GetCurrentMap()     // trả shallow copy (lock)

    // ─── BackgroundService entry point ───────────────────
    protected override async Task ExecuteAsync(CancellationToken ct)

    // ─── Tick ────────────────────────────────────────────
    private async Task TickAsync(CancellationToken ct)

    // ─── Sensor layer ────────────────────────────────────
    private async Task UpdateAgvStateAsync()     // Modbus read → _currentState
    private async Task UpdateObstaclesAsync()    // Vision → _currentMap

    // ─── State handlers ──────────────────────────────────
    private async Task HandleSpinningAsync()
    private async Task HandleMovingAsync()

    // ─── Steering helpers ────────────────────────────────
    private double ComputeTargetAngle(Point waypoint)   // atan2, returns degrees
    private double NormalizeAngle(double deg)            // -180..180
    private double DistanceToWaypoint(Point waypoint)    // Euclidean mm

    // ─── Path helpers ────────────────────────────────────
    private bool IsNextWaypointBlocked()
    private async Task<bool> TryReplanAsync()   // bool = success
    private void AdvanceWaypoint()               // RemoveAt(0) + guard

    // ─── Motor helper ────────────────────────────────────
    private async Task SendMotorCommandAsync(int leftRpm, int rightRpm, CommandCode cmd)

    // ─── Trip lifecycle ──────────────────────────────────
    private async Task OnTripStartedAsync()     // LogPathAsync("started")
    private async Task OnTripCompletedAsync()   // LogPathAsync("completed")
    private async Task OnTripErrorAsync(string msg)   // LogSystemEvent("error")
}
```

---

## 6. Vòng lặp chính — chi tiết từng bước

### ExecuteAsync

```csharp
protected override async Task ExecuteAsync(CancellationToken ct)
{
    _currentMap.InitStaticWalls();
    await _modbus.ConnectAsync();

    while (!ct.IsCancellationRequested)
    {
        try   { await TickAsync(ct); }
        catch (Exception ex) { _logger.LogError(ex, "Tick error"); }

        await Task.Delay(100, ct);
    }
}
```

### TickAsync — 7 bước tuần tự

```
1. UpdateAgvStateAsync()
   └─ ReadStatusAsync() → cập nhật _currentState (lock)

2. UpdateObstaclesAsync()
   └─ GetLatestDetectionsAsync() → null? bỏ qua
   └─ ClearDynamicObstacles()
   └─ foreach detection có DistanceMeters:
        obstacle_x_mm = agv_x + (distance_m×1000 + 300) × cos(heading_rad)
        obstacle_y_mm = agv_y + (distance_m×1000 + 300) × sin(heading_rad)
        (gx, gy) = WorldToGrid(obstacle_x_mm, obstacle_y_mm)
        if (gx != -1) → SetObstacle(gx, gy)

3. Đọc _state (lock)
   └─ Idle     → return (không làm gì)
   └─ Spinning → HandleSpinningAsync()
   └─ Moving   → HandleMovingAsync()

(Bước 4–7 nằm trong HandleSpinningAsync / HandleMovingAsync)
```

### HandleSpinningAsync

```
targetAngle = ComputeTargetAngle(_waypoints[0])   // atan2 degrees
angleDiff   = NormalizeAngle(targetAngle - _currentState.HeadingDegrees)

if |angleDiff| < 10°:
    SendMotorCommandAsync(0, 0, Stop)
    _state = Moving
    return   // KHÔNG gửi MOVE trong tick này

angleDiff > 0 → SendMotorCommandAsync(-200, +200, Move)   // spin trái
angleDiff < 0 → SendMotorCommandAsync(+200, -200, Move)   // spin phải
```

### HandleMovingAsync

```
dist = DistanceToWaypoint(_waypoints[0])

if dist < 250:
    AdvanceWaypoint()                          // RemoveAt(0) với guard
    if _waypoints.Count == 0:
        SendMotorCommandAsync(0, 0, Stop)
        await OnTripCompletedAsync()
        _state = Idle
        _target = null
    else:
        _state = Spinning
    return

if IsNextWaypointBlocked():
    if UtcNow - _lastReplanTime > 500ms:
        success = await TryReplanAsync()
        _lastReplanTime = UtcNow
        if success:
            _state = Spinning
        else:
            SendMotorCommandAsync(0, 0, Stop)
            await OnTripErrorAsync("No path found")
            _state = Spinning   // giữ target, retry tick sau
    return

SendMotorCommandAsync(300, 300, Move)
```

---

## 7. Công thức quan trọng

### Vision → Grid

```
heading_rad   = _currentState.HeadingRadians
obstacle_x_mm = _currentState.PositionX + (detection.DistanceMeters * 1000 + 300) * Math.Cos(heading_rad)
obstacle_y_mm = _currentState.PositionY + (detection.DistanceMeters * 1000 + 300) * Math.Sin(heading_rad)
(gx, gy)      = _currentMap.WorldToGrid(obstacle_x_mm, obstacle_y_mm)
// Chỉ gọi SetObstacle nếu gx != -1 (in bounds)
```

### Steering angle

```csharp
private double ComputeTargetAngle(Point waypoint)
{
    double wpCenterX = waypoint.X * GridMap.CellSizeMm + GridMap.CellSizeMm / 2.0;
    double wpCenterY = waypoint.Y * GridMap.CellSizeMm + GridMap.CellSizeMm / 2.0;
    double dx = wpCenterX - _currentState.PositionX;
    double dy = wpCenterY - _currentState.PositionY;
    return Math.Atan2(dy, dx) * 180.0 / Math.PI;   // degrees
}

private double NormalizeAngle(double deg)
{
    while (deg >  180) deg -= 360;
    while (deg < -180) deg += 360;
    return deg;
}
```

### Euclidean distance

```csharp
private double DistanceToWaypoint(Point waypoint)
{
    double wpCenterX = waypoint.X * GridMap.CellSizeMm + GridMap.CellSizeMm / 2.0;
    double wpCenterY = waypoint.Y * GridMap.CellSizeMm + GridMap.CellSizeMm / 2.0;
    double dx = wpCenterX - _currentState.PositionX;
    double dy = wpCenterY - _currentState.PositionY;
    return Math.Sqrt(dx * dx + dy * dy);
}
```

---

## 8. Trip lifecycle — Logging

```
StartTrip()
  └─ OnTripStartedAsync()
       └─ LogPathAsync(status="started", start, end, waypoints)
       └─ _pathId = returned id

TryReplanAsync() được gọi
  └─ LogSystemEventAsync("info", "AgvOrchestrator", "replan", "Replanning due to blocked waypoint")

OnTripErrorAsync()
  └─ LogSystemEventAsync("error", "AgvOrchestrator", "no_path", message)

AGV đến đích
  └─ OnTripCompletedAsync()
       └─ LogPathAsync(status="completed", duration = UtcNow - _tripStartTime)
```

> **Fire-and-forget**: Tất cả log đều dùng `_ = Task.Run(...)` hoặc không `await`.
> DB failure không được phép crash control loop.

---

## 8. Constants

| Hằng số | Giá trị | Ý nghĩa |
|---|---|---|
| Spin speed | 200 RPM | Tốc độ quay tại chỗ |
| Move speed | 300 RPM | Tốc độ đi thẳng |
| Heading threshold | 10° | Coi là "đã thẳng hướng" |
| Waypoint reached | 250mm | Euclidean distance đến tâm cell |
| Replan cooldown | 500ms | Thời gian tối thiểu giữa 2 lần replan |
| Camera offset | 300mm | Offset từ AGV center đến camera |

Nên định nghĩa tất cả là `private const` ở đầu class, không hardcode trong logic.

---

## 9. Thread safety — quy tắc

```
_lock bao toàn bộ:
  - _state
  - _target
  - _waypoints
  - _currentState
  - _currentMap
  - _tripStartTime / _pathId / _lastReplanTime

GetCurrentState() → lock → return new AgvState { ... }   // shallow copy
GetCurrentMap()   → lock → return new GridMap(existing)  // copy grid data
```

> HTTP thread (Controller) và Background thread (ExecuteAsync) đều truy cập
> shared fields → **mọi read/write shared field đều phải trong `lock(_lock)`**.

---

## 10. Checklist triển khai

- [ ] Tạo `enum OrchestratorState { Idle, Spinning, Moving }`
- [ ] Khai báo đủ fields (mục 4)
- [ ] Constructor nhận đủ 5 DI params
- [ ] `ExecuteAsync`: `InitStaticWalls` + `ConnectAsync` + loop với try/catch
- [ ] `TickAsync`: 3 bước (UpdateState, UpdateObstacles, dispatch handler)
- [ ] `UpdateAgvStateAsync`: Modbus read + lock update
- [ ] `UpdateObstaclesAsync`: Vision poll + công thức obstacle mm → grid
- [ ] `HandleSpinningAsync`: atan2 + normalize + threshold 10° + spin 200 RPM
- [ ] `HandleMovingAsync`: distance check + arrived logic + replan logic + MOVE 300 RPM
- [ ] `ComputeTargetAngle(Point)`: atan2 với waypoint center mm
- [ ] `NormalizeAngle`: while loop -180..180
- [ ] `DistanceToWaypoint(Point)`: Euclidean với waypoint center mm
- [ ] `IsNextWaypointBlocked()`: check `_currentMap.GetCell(wp.X, wp.Y) != Empty`
- [ ] `TryReplanAsync()`: A* từ current grid pos → _target, catch exception
- [ ] `AdvanceWaypoint()`: guard `Count > 0` trước `RemoveAt(0)`
- [ ] `SendMotorCommandAsync`: wrapper cho `_modbus.WriteMotorCommandAsync`
- [ ] `OnTripStartedAsync`, `OnTripCompletedAsync`, `OnTripErrorAsync`: log fire-and-forget
- [ ] `StartTrip`, `StopTrip`, `EmergencyStop`: lock + update state
- [ ] `GetCurrentState`, `GetCurrentMap`: lock + return copy
- [ ] `dotnet build` → 0 errors
