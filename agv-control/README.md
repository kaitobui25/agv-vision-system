# AGV Control Server

> C# ASP.NET Web API — Path planning, orchestration, Vision AI integration, Modbus motor control.

## Architecture

```
AgvControl/
├── Controllers/
│   ├── AgvController.cs          # REST endpoints (start, stop, status, map)
│   └── HealthController.cs       # Health check (Vision AI + Modbus + DB)
├── Services/
│   ├── VisionClient.cs           # HttpClient → Vision AI (GET /detect/latest)
│   ├── ModbusClient.cs           # Modbus TCP client → hardware-sim (port 502)
│   ├── PathPlanner.cs            # A* algorithm on 40x20 grid
│   └── AgvOrchestrator.cs        # BackgroundService: Vision → Path → Modbus → Log
├── Models/
│   ├── DetectionResult.cs        # Vision AI response model
│   ├── AgvState.cs               # Current AGV state (position, speed, battery)
│   ├── GridMap.cs                # Static warehouse layout + dynamic obstacles
│   └── ModbusRegisters.cs        # Register address constants (from 04_MODBUS_REGISTER_MAP)
└── Data/
    └── DbLogger.cs              # Npgsql direct (no EF Core)
```

## API Endpoints

| Method | Endpoint              | Description                                 |
| ------ | --------------------- | ------------------------------------------- |
| GET    | `/health`             | Health check (Vision AI, Modbus, DB status) |
| POST   | `/agv/start`          | Start trip with `{target_x, target_y}`      |
| POST   | `/agv/stop`           | Gradual stop                                |
| POST   | `/agv/emergency-stop` | Emergency stop                              |
| GET    | `/agv/status`         | Current state (position, speed, battery)    |
| GET    | `/agv/map`            | Grid map with AGV position + obstacles      |

## Grid Map

- Warehouse: 20 x 10 meters
- Cell size: 500mm
- Grid: 40 x 20 cells
- Static walls defined in code/JSON + dynamic obstacles from Vision AI

## Control Flow

```
BackgroundService (polling every 100ms):
│
├── 1. Poll Vision AI → GET /detect/latest
│      └── {obstacles: [{class: "box", distance_meters: 2.0}]}
│
├── 2. Convert detection → grid coordinates
│      └── heading_rad = (heading_modbus / 10) × π / 180
│      └── obstacle_x = agv_x + (distance + CAMERA_OFFSET) × cos(heading_rad)
│      └── obstacle_y = agv_y + (distance + CAMERA_OFFSET) × sin(heading_rad)
│      └── grid_cell = (obstacle_x / 500, obstacle_y / 500) ← with bounds check
│
├── 3. Run A* pathfinding → List<Point> waypoints
│
├── 4. Convert waypoints → motor commands
│      └── Write Modbus registers [1000, 1001, 1002]
│
├── 5. Read Modbus feedback
│      └── Poll registers [2000-2007] (status, position, battery)
│
└── 6. Log to PostgreSQL
       └── INSERT into paths, system_logs
```

## Key Constants

| Constant        | Value | Unit | Source                     |
| --------------- | ----- | ---- | -------------------------- |
| CAMERA_OFFSET   | 300   | mm   | 04_MODBUS_REGISTER_MAP.md  |
| CELL_SIZE       | 500   | mm   | Grid map specification     |
| GRID_WIDTH      | 40    | cells| 20000mm / 500mm            |
| GRID_HEIGHT     | 20    | cells| 10000mm / 500mm            |
| POLL_INTERVAL   | 100   | ms   | Control loop frequency     |

## Build

```bash
cd agv-control/AgvControl
dotnet restore
dotnet build
```

## Run

```bash
dotnet run
```

Swagger UI: http://localhost:5034/swagger

## NuGet Packages

| Package                 | Purpose              |
| ----------------------- | -------------------- |
| Swashbuckle.AspNetCore  | Swagger UI (built-in)|
| NModbus                 | Modbus TCP client    |
| Npgsql                  | PostgreSQL driver    |

## Related Documents

- [04_MODBUS_REGISTER_MAP.md](../docs/04_MODBUS_REGISTER_MAP.md) — Shared Modbus contract
- [01_PROJECT_NOTES.md](../docs/01_PROJECT_NOTES.md) — Project decisions and progress
- [02_ARCHITECTURE.md](../docs/02_ARCHITECTURE.md) — System architecture
