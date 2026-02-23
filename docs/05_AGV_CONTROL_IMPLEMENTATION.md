# Implementation Plan — agv-control (C# ASP.NET Web API)

Build the AGV Control Server: path planning (A*), Vision AI integration, Modbus motor control, PostgreSQL logging. All code goes in `agv-control/AgvControl/`.

---

## Implementation Order

Build in dependency order — check off each step after `dotnet build` succeeds:

- [ ] **Step 1**: NuGet packages (csproj) → `dotnet restore`
- [ ] **Step 2**: Models (ModbusRegisters, AgvState, DetectionResult, GridMap) → `dotnet build`
- [ ] **Step 3**: appsettings.json (connection settings) → `dotnet build`
- [ ] **Step 4**: Services/VisionClient.cs → `dotnet build`
- [ ] **Step 5**: Services/ModbusClient.cs → `dotnet build`
- [ ] **Step 6**: Services/PathPlanner.cs → `dotnet build`
- [ ] **Step 7**: Data/DbLogger.cs → `dotnet build`
- [ ] **Step 8**: Services/AgvOrchestrator.cs → `dotnet build`
- [ ] **Step 9**: Controllers (HealthController, AgvController) → `dotnet build`
- [ ] **Step 10**: Program.cs (DI wiring) → `dotnet build`

---

## Proposed Changes

### 1. NuGet Packages

#### [MODIFY] AgvControl.csproj

Add 2 packages:

```xml
<PackageReference Include="NModbus" Version="3.0.81" />
<PackageReference Include="Npgsql" Version="10.0.1" />
```

---

### 2. Models (no dependencies)

#### [NEW] Models/ModbusRegisters.cs

Constants matching `docs/04_MODBUS_REGISTER_MAP.md`:

```csharp
public static class ModbusRegisters
{
    // Holding Registers (R/W) — C# writes, C++ reads
    public const ushort LeftMotorSpeed  = 1000;
    public const ushort RightMotorSpeed = 1001;
    public const ushort Command         = 1002;

    // Input Registers (Read Only) — C++ updates, C# polls
    public const ushort Status          = 2000;
    public const ushort ActualLeftSpeed = 2001;
    // ... all 2000-2007

    // Command codes
    public enum CommandCode : ushort { Idle=0, Move=1, Stop=2, EmergencyStop=3, Reset=4 }
    public enum StatusCode  : ushort { Idle=0, Moving=1, Stopped=2, EStopped=3, Error=4 }
    public enum ErrorCode   : ushort { Ok=0, MotorOverload=1, BatteryCritical=2, ... }
}
```

#### [NEW] Models/AgvState.cs

Current AGV state from Modbus input registers:

```csharp
public class AgvState
{
    public int PositionX { get; set; }       // mm
    public int PositionY { get; set; }       // mm
    public double HeadingDegrees { get; set; } // 0-359.9°
    public double HeadingRadians => (HeadingDegrees) * Math.PI / 180.0;
    public int ActualLeftSpeed { get; set; }
    public int ActualRightSpeed { get; set; }
    public int BatteryLevel { get; set; }
    public StatusCode Status { get; set; }
    public ErrorCode Error { get; set; }
}
```

#### [NEW] Models/DetectionResult.cs

Maps Vision AI JSON response (from `GET /detect/latest`):

```csharp
public class VisionResponse
{
    public List<Detection> Detections { get; set; }
    public int ProcessingTimeMs { get; set; }
    public int TotalObjects { get; set; }
}

public class Detection
{
    public string ObjectClass { get; set; }
    public double Confidence { get; set; }
    public double? DistanceMeters { get; set; }  // From pinhole camera model
}
```

#### [NEW] Models/GridMap.cs

40×20 grid with cell types:

```csharp
public class GridMap
{
    public const int Width = 40;           // 20000mm / 500mm
    public const int Height = 20;          // 10000mm / 500mm
    public const int CellSizeMm = 500;
    public const int CameraOffsetMm = 300;

    public CellType[,] Grid { get; }       // [x, y]

    public void InitStaticWalls();          // Load warehouse walls
    public void ClearDynamicObstacles();    // Reset before each cycle
    public bool SetObstacle(int x, int y); // With bounds check!
    public Point WorldToGrid(double xMm, double yMm);
}

public enum CellType { Empty=0, StaticWall=1, DynamicObstacle=2, AgvPosition=3 }
```

---

### 3. Configuration

#### [MODIFY] appsettings.json

Add connection settings:

```json
{
  "VisionAi": {
    "BaseUrl": "http://localhost:8000",
    "TimeoutMs": 2000
  },
  "Modbus": {
    "Host": "127.0.0.1",
    "Port": 502,
    "UnitId": 1,
    "PollIntervalMs": 100
  },
  "Database": {
    "ConnectionString": "Host=localhost;Port=5432;Database=agv_system;Username=agv_user;Password=agv_pass"
  }
}
```

---

### 4. Services (depend on Models)

#### [NEW] Services/VisionClient.cs

REST client to Vision AI:

```csharp
public interface IVisionClient
{
    Task<VisionResponse?> GetLatestDetectionsAsync();
    Task<bool> HealthCheckAsync();
}

public class VisionClient : IVisionClient
{
    private readonly HttpClient _http;

    // GET http://localhost:8000/detect/latest
    // Deserializes JSON → VisionResponse
    // Timeout 2s, returns null on failure (graceful degradation)
}
```

- Registered as `Singleton` via `IHttpClientFactory`
- Uses `System.Text.Json` with snake_case naming policy (match Python)

#### [NEW] Services/ModbusClient.cs

Modbus TCP client to hardware-sim:

```csharp
public interface IModbusClient
{
    Task ConnectAsync();
    Task WriteMotorCommandAsync(short leftRpm, short rightRpm, CommandCode cmd);
    Task<AgvState> ReadStatusAsync();
    bool IsConnected { get; }
}

public class ModbusClient : IModbusClient, IDisposable
{
    private TcpClient _tcpClient;
    private IModbusMaster _master;

    // WriteMotorCommandAsync → FC16 write holding registers [1000-1002]
    // ReadStatusAsync → FC04 read input registers [2000-2007]
    //   heading = register[2005] / 10.0  (convert 0-3599 → 0.0-359.9°)
    // Auto-reconnect on failure
}
```

- Registered as `Singleton` (one TCP connection)

#### [NEW] Services/PathPlanner.cs

A* pathfinding:

```csharp
public interface IPathPlanner
{
    List<Point> FindPath(GridMap map, Point start, Point goal);
}

public class PathPlanner : IPathPlanner
{
    // Standard A* with 8-directional movement
    // Heuristic: Octile distance
    // Returns empty list if no path found
    // Uses PriorityQueue<T> (.NET 6+)
}
```

- Registered as `Singleton` (stateless)

#### [NEW] Services/AgvOrchestrator.cs

The main control loop — BackgroundService:

```csharp
public class AgvOrchestrator : BackgroundService
{
    // Constructor DI: IVisionClient, IModbusClient, IPathPlanner, IDbLogger, ILogger

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Read AGV state from Modbus
            // 2. Poll Vision AI → detections
            // 3. Convert detections → grid obstacles
            //    - Radian: heading_modbus / 10 × π / 180
            //    - Camera offset: distance + 300mm
            //    - Bounds check before grid[x,y]
            // 4. If has target → A* pathfinding
            // 5. Convert next waypoint → motor speeds
            // 6. Write motor command via Modbus
            // 7. Log to DB

            await Task.Delay(100, stoppingToken);  // 100ms poll
        }
    }

    // Public methods for Controllers:
    public void StartTrip(double targetX, double targetY);
    public void StopTrip();
    public void EmergencyStop();
    public AgvState GetCurrentState();
    public GridMap GetCurrentMap();
}
```

- Registered as `AddHostedService<AgvOrchestrator>()` + `AddSingleton<AgvOrchestrator>()`

---

### 5. Data Layer

#### [NEW] Data/DbLogger.cs

Direct PostgreSQL logging via Npgsql:

```csharp
public interface IDbLogger
{
    Task LogPathAsync(PathRecord record);
    Task LogSystemEventAsync(string level, string component, string eventType,
                              string message, object? details = null);
    Task<bool> HealthCheckAsync();
}

public class DbLogger : IDbLogger
{
    private readonly string _connectionString;

    // INSERT INTO paths (start_x, start_y, end_x, end_y, waypoints, status, ...)
    // INSERT INTO system_logs (level, component, event_type, message, details,
    //                          agv_speed_mms, battery_percentage, position_x, position_y)
    // Uses parameterized queries (no SQL injection)
    // Fire-and-forget: DB failure should NOT crash control loop
}
```

- Registered as `Singleton`

---

### 6. Controllers

#### [NEW] Controllers/HealthController.cs

```csharp
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    // GET /health → checks Vision AI, Modbus, DB connectivity
    // Returns { vision_ai: "ok"|"error", modbus: "ok"|"error", database: "ok"|"error" }
}
```

#### [NEW] Controllers/AgvController.cs

```csharp
[ApiController]
[Route("agv")]
public class AgvController : ControllerBase
{
    // POST /agv/start     → body: {target_x, target_y} → 202 Accepted
    // POST /agv/stop      → gradual stop → 200 OK
    // POST /agv/emergency-stop → immediate stop → 200 OK
    // GET  /agv/status    → AgvState JSON
    // GET  /agv/map       → GridMap JSON (40×20 array + AGV position)
}
```

---

### 7. DI Wiring

#### [MODIFY] Program.cs

```csharp
// Configuration
builder.Services.Configure<VisionAiSettings>(builder.Configuration.GetSection("VisionAi"));
builder.Services.Configure<ModbusSettings>(builder.Configuration.GetSection("Modbus"));
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database"));

// HttpClient for Vision AI
builder.Services.AddHttpClient<IVisionClient, VisionClient>();

// Services
builder.Services.AddSingleton<IModbusClient, ModbusClient>();
builder.Services.AddSingleton<IPathPlanner, PathPlanner>();
builder.Services.AddSingleton<IDbLogger, DbLogger>();

// Background Service (Orchestrator)
builder.Services.AddSingleton<AgvOrchestrator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgvOrchestrator>());
```

---

## Verification Plan

### Automated Build Check

```bash
cd agv-control/AgvControl
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

### Manual Verification — Swagger UI

After `dotnet run`:

1. Open http://localhost:5034/swagger
2. Verify all 6 endpoints visible
3. Test GET `/health` → expect JSON
4. Test GET `/agv/status` → expect default state
5. Test GET `/agv/map` → expect 40×20 grid with static walls

> **Note**: Full integration test requires all modules running (Vision AI + Modbus + DB). Services are designed with graceful degradation for standalone testing.
