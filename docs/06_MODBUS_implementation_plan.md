# Modbus Implementation Plan — Từ Ý Tưởng Đến Test Hoàn Thiện
---

## Phase 0 — Hiểu Bài Toán (Concept)

### Câu hỏi cần trả lời trước khi viết bất kỳ dòng code nào

| # | Câu hỏi | Trả lời cho project này |
|---|---------|--------------------------|
| 1 | Tôi là ai trong giao tiếp? | C# = **Master/Client** (chủ động gọi). C++ = **Slave/Server** (ngồi chờ) |
| 2 | Nói chuyện qua đường nào? | **Modbus TCP**, port 502, qua localhost (cùng máy lúc dev) |
| 3 | Tôi cần đọc gì? | 8 input registers (2000-2007): vị trí, tốc độ, battery, trạng thái, lỗi |
| 4 | Tôi cần ghi gì? | 3 holding registers (1000-1002): tốc độ motor trái/phải + lệnh |
| 5 | Đọc bao lâu 1 lần? | 100ms (PollIntervalMs trong appsettings.json) |
| 6 | Timeout bao lâu? | 1000ms (TimeoutMs trong appsettings.json) |
| 7 | Mất kết nối thì sao? | Log warning, retry kết nối, AGV dừng an toàn |

### Topology

```
┌─────────────────────┐     Modbus TCP      ┌─────────────────────┐
│  C# agv-control     │     port 502        │  C++ hardware-sim   │
│  (Master/Client)    │ ──────────────────→ │  (Slave/Server)     │
│                     │                      │                     │
│  GHI: FC16          │  Holding Reg         │  ĐỌC holding →     │
│  [1000-1002]        │  1000,1001,1002      │  điều khiển motor   │
│                     │                      │                     │
│  ĐỌC: FC04         │  Input Reg           │  CẬP NHẬT input →  │
│  [2000-2007]        │  2000-2007           │  trạng thái AGV     │
└─────────────────────┘                      └─────────────────────┘
```

---

## Phase 1 — Xây C++ Hardware Simulator (Slave/Server)

### Tại sao phải làm trước C#?

> Không có server → client kết nối vào đâu? Phải có "người trả lời" trước khi "người hỏi" bắt đầu hỏi.

### Tư duy: "Server này cần làm gì?"

```
1. Lắng nghe TCP port 502
2. Nhận request FC16 (Write Multiple) → lưu giá trị vào holding registers
3. Nhận request FC03 (Read Holding) → trả về giá trị holding registers
4. Nhận request FC04 (Read Input) → trả về giá trị input registers
5. Cập nhật input registers mỗi 50ms (simulate vật lý: vị trí, heading...)
6. Safety watchdog: không nhận lệnh 5s → auto-stop
```

### Deliverables

#### [NEW] `hardware-sim/CMakeLists.txt`

- Build system cho C++ project
- Dependency: `libmodbus` (Modbus TCP library cho C++)

#### [NEW] `hardware-sim/src/main.cpp`

- Entry point: tạo Modbus server, chạy simulation loop
- Các thành phần:
  - **Register storage**: 2 mảng `uint16_t` cho holding (3 registers) và input (8 registers)
  - **Simulation loop (50ms)**: tính vị trí mới dựa trên differential drive kinematics
  - **Watchdog timer**: nếu 5s không nhận lệnh → set status = E_STOPPED

### Chi tiết Simulation (Differential Drive)

Công thức đã có sẵn trong [04_MODBUS_REGISTER_MAP.md](file:///d:/Phong/02_RobotLearn/agv-vision-system/docs/04_MODBUS_REGISTER_MAP.md):

```
v_left  = left_speed  × wheel_radius × 2π / 60    (mm/s)
v_right = right_speed × wheel_radius × 2π / 60    (mm/s)
v_linear  = (v_left + v_right) / 2                 (mm/s)
v_angular = (v_right - v_left) / wheel_base         (rad/s)

position_x += v_linear × cos(heading) × Δt
position_y += v_linear × sin(heading) × Δt
heading    += v_angular × Δt
```

---

## Phase 2 — Test C++ Server Độc Lập

### Tại sao test riêng trước khi viết C# client?

> Nếu server sai → client sẽ đọc sai → debug 2 đầu cùng lúc = nightmare.
> Test server trước → khi viết client, biết chắc server đúng.

### Cách test

1. **Build và chạy** C++ server:
   ```bash
   cd hardware-sim/build
   cmake .. && cmake --build .
   ./motor_controller
   ```

2. **Dùng tool Modbus bên ngoài** để test (không cần code C#):
   - Option A: Cài [Modbus Poll](https://www.modbustools.com/modbus_poll.html) (GUI Windows)
   - Option B: Dùng Python script đơn giản với `pymodbus`

3. **Kiểm tra checklist**:
   - [ ] Kết nối TCP port 502 thành công
   - [ ] Ghi holding register 1000 = 500 → đọc lại = 500
   - [ ] Đọc input register 2000 → trả về status (0 = IDLE)
   - [ ] Ghi command = 1 (MOVE) → đọc status chuyển sang 1 (MOVING)
   - [ ] Đọc position_x, position_y → giá trị thay đổi khi MOVING
   - [ ] Không ghi lệnh 5s → status = E_STOPPED, error = COMM_TIMEOUT

---

## Phase 3 — Thiết Kế Interface C# ModbusClient

### Tư duy: "Người dùng (AgvOrchestrator) cần gì?"

```csharp
// AgvOrchestrator chỉ cần biết 4 thứ:
await modbusClient.ConnectAsync();                               // 1. Kết nối
await modbusClient.WriteMotorCommandAsync(500, 500, Move);       // 2. Ghi lệnh
AgvState state = await modbusClient.ReadStatusAsync();           // 3. Đọc trạng thái
bool ok = modbusClient.IsConnected;                              // 4. Kiểm tra kết nối
```

Orchestrator **KHÔNG cần biết**: TCP socket, function code, register address, byte order.

### Interface đã thiết kế sẵn (trong 05_AGV_CONTROL_IMPLEMENTATION.md)

```csharp
public interface IModbusClient
{
    Task ConnectAsync();
    Task WriteMotorCommandAsync(short leftRpm, short rightRpm, CommandCode cmd);
    Task<AgvState> ReadStatusAsync();
    bool IsConnected { get; }
}
```

→ Không cần thay đổi interface. Chỉ cần implement.

---

## Phase 4 — Implement C# ModbusClient

### Tư duy: "Chia method, mỗi method làm đúng 1 việc"

#### [NEW] `Services/ModbusClient.cs`

**Constructor** — nhận config + logger, chưa kết nối:
```csharp
public ModbusClient(IOptions<ModbusSettings> settings, ILogger<ModbusClient> logger)
{
    _host = settings.Value.Host;       // "127.0.0.1"
    _port = settings.Value.Port;       // 502
    _unitId = settings.Value.UnitId;   // 1
    _timeoutMs = settings.Value.TimeoutMs;  // 1000
}
```

**ConnectAsync()** — mở TCP socket, tạo NModbus master:
```csharp
public async Task ConnectAsync()
{
    _tcpClient = new TcpClient();
    await _tcpClient.ConnectAsync(_host, _port);
    _tcpClient.ReceiveTimeout = _timeoutMs;
    _tcpClient.SendTimeout = _timeoutMs;

    var factory = new ModbusFactory();
    _master = factory.CreateMaster(_tcpClient);
    _master.Transport.ReadTimeout = _timeoutMs;
    _master.Transport.WriteTimeout = _timeoutMs;
}
```

**WriteMotorCommandAsync()** — FC16 ghi 3 holding registers:
```csharp
public async Task WriteMotorCommandAsync(short leftRpm, short rightRpm, CommandCode cmd)
{
    // Signed → Unsigned cast (Modbus truyền ushort, motor speed là signed)
    ushort[] values = new ushort[]
    {
        (ushort)leftRpm,    // Register 1000
        (ushort)rightRpm,   // Register 1001
        (ushort)cmd         // Register 1002
    };

    await _master.WriteMultipleRegistersAsync(
        _unitId,
        ModbusRegisters.HoldingStart,  // 1000
        values
    );
}
```

**ReadStatusAsync()** — FC04 đọc 8 input registers → map vào AgvState:
```csharp
public async Task<AgvState> ReadStatusAsync()
{
    ushort[] regs = await _master.ReadInputRegistersAsync(
        _unitId,
        ModbusRegisters.InputStart,  // 2000
        ModbusRegisters.InputCount   // 8
    );

    return new AgvState
    {
        Status          = (StatusCode)regs[0],
        ActualLeftSpeed = (short)regs[1],    // Cast ngược về signed
        ActualRightSpeed= (short)regs[2],
        PositionX       = (short)regs[3],
        PositionY       = (short)regs[4],
        HeadingDegrees  = regs[5] / 10.0,    // 0-3599 → 0.0-359.9
        BatteryLevel    = regs[6],
        Error           = (ErrorCode)regs[7],
        LastUpdated     = DateTime.UtcNow
    };
}
```

**Reconnect logic** — bọc try/catch, log lỗi, không crash:
```csharp
// Trong mỗi method đọc/ghi:
catch (Exception ex)
{
    _logger.LogWarning("Modbus communication failed: {Message}", ex.Message);
    _isConnected = false;
    await TryReconnectAsync();  // Thử kết nối lại
    throw;  // Để caller (Orchestrator) biết lần này thất bại
}
```

### Settings class (cùng file hoặc riêng)

```csharp
public class ModbusSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 502;
    public byte UnitId { get; set; } = 1;
    public int TimeoutMs { get; set; } = 1000;
    public int PollIntervalMs { get; set; } = 100;
}
```

---

## Phase 5 — Test C# ModbusClient vs C++ Server

### Bước 5.1: Build C# kiểm tra syntax
```bash
cd agv-control/AgvControl
dotnet build
```
Kỳ vọng: `Build succeeded. 0 Warning(s) 0 Error(s)`

### Bước 5.2: Unit Test với Mock (không cần server)

#### [NEW] `AgvControl.Tests/ModbusClientTests.cs`

Test những gì **không cần kết nối thật**:

- Constructor không throw exception
- `IsConnected` mặc định = `false`
- `WriteMotorCommandAsync` với giá trị signed (-500) → unsigned cast đúng
- `ReadStatusAsync` parse heading 2345 → 234.5° đúng
- `ReadStatusAsync` parse signed speed -300 đúng

```bash
cd agv-control
dotnet test
```

### Bước 5.3: Integration Test (cần C++ server chạy)

**Tiền điều kiện**: C++ hardware-sim đang chạy trên port 502

1. Chạy C++ server:
   ```bash
   cd hardware-sim/build
   ./motor_controller
   ```

2. Chạy integration test hoặc console app test:
   ```
   Connect → IsConnected = true
   Write MOVE (500, 500) → không throw
   Đợi 500ms
   Read status → Status = Moving, position thay đổi
   Write STOP → không throw
   Đợi 1s
   Read status → Status = Stopped
   ```

---

## Phase 6 — Error Handling & Edge Cases

### Tư duy: "Điều gì có thể sai?"

| Tình huống | Xử lý |
|---|---|
| C++ server chưa chạy → ConnectAsync() fail | Log error, IsConnected = false, Orchestrator bỏ qua cycle này |
| Đang đọc/ghi → mất kết nối | Catch `IOException`, set IsConnected = false, thử reconnect |
| Server trả timeout (>1000ms) | Catch `TimeoutException`, log warning |
| Motor speed ngoài range (-1000 → 1000) | Clamp trong WriteMotorCommandAsync trước khi ghi |
| Heading register = 3600 (lẽ ra max 3599) | Modulo 3600 khi parse → tránh HeadingDegrees = 360.0 |

### Reconnect Strategy

```
Lần 1: reconnect ngay
Lần 2: đợi 1s rồi reconnect
Lần 3: đợi 2s rồi reconnect
Lần 4+: đợi 5s rồi reconnect
→ Không exponential backoff phức tạp, không circuit breaker — KISS.
```

---

## Phase 7 — Kiểm Tra Toàn Diện

### Checklist cuối cùng

| # | Test | Phương pháp | Pass Criteria |
|---|------|-------------|---------------|
| 1 | C# build thành công | `dotnet build` | 0 errors, 0 warnings |
| 2 | Unit tests pass | `dotnet test` | All tests passed |
| 3 | C++ server chạy | `./motor_controller` | "Listening on port 502" |
| 4 | C# connect thành công | Log output | "Connected to Modbus at 127.0.0.1:502" |
| 5 | Write MOVE command | Log output | Motor registers = 500, 500, 1 |
| 6 | Read status | Log output | AgvState có PositionX, PositionY thay đổi |
| 7 | Emergency stop | Ghi cmd=3 | Status = EStopped, motor = 0 |
| 8 | Mất kết nối | Kill C++ server | Log warning, IsConnected = false, không crash |
| 9 | Auto reconnect | Restart C++ server | Log "Reconnected", IsConnected = true |

---

## Thứ Tự Thực Hiện (Summary)

```
Phase 0: Hiểu bài toán              → Đã xong (tài liệu + models sẵn)
Phase 1: C++ hardware-sim           → Tạo Modbus TCP server
Phase 2: Test C++ độc lập           → Dùng Modbus tool/script kiểm tra
Phase 3: Thiết kế interface C#      → Đã xong (IModbusClient đã define)
Phase 4: Implement ModbusClient.cs  → Code 4 methods + settings class
Phase 5: Test C# (build + unit)     → dotnet build + dotnet test
Phase 6: Error handling             → Reconnect, timeout, clamp
Phase 7: Integration test           → C++ server + C# client chạy cùng nhau
```

> [!IMPORTANT]
> **Chicken-and-egg**: C++ server phải có trước → C# client mới test được.
> Nhưng có thể **build** và **unit test** C# mà không cần C++ server (dùng mock).
> Chọn path nào trước tùy bạn — nhưng integration test cuối cùng **bắt buộc** cần cả hai.
