// ==========================================================================
// ModbusClient.cs — Modbus TCP client for C++ hardware-sim
// ==========================================================================
// Connects to the C++ hardware simulator (Modbus TCP server, port 502).
// Provides 3 operations for AgvOrchestrator:
//   1. ConnectAsync()            — open TCP connection
//   2. WriteMotorCommandAsync()  — FC16 write holding registers 1000-1002
//   3. ReadStatusAsync()         — FC04 read input registers 2000-2007
//
// Design:
// - S: Only responsible for Modbus TCP communication
// - O: Interface allows swapping with mock for unit tests
// - D: AgvOrchestrator depends on IModbusClient, not this concrete class
// - KISS: NModbus sync API wrapped in Task.Run (NModbus 3.x has no true async)
// - Graceful degradation: auto-reconnect on failure, never crashes Orchestrator
//
// Register contract: docs/04_MODBUS_REGISTER_MAP.md
// ==========================================================================


using System.Net.Sockets;
using AgvControl.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NModbus;

namespace AgvControl.Services;

// ---------------------------------------------------------------------------
// Configuration — bound from appsettings.json "Modbus" section
// ---------------------------------------------------------------------------
public class ModbusSettings
{
    public string Host          { get; set; } = "127.0.0.1";
    public int    Port          { get; set; } = 502;
    public byte   UnitId        { get; set; } = 1;
    public int    TimeoutMs     { get; set; } = 1000;
    public int    PollIntervalMs{ get; set; } = 100;  // used by AgvOrchestrator
}

// ---------------------------------------------------------------------------
// Interface — contract for dependency injection
// ---------------------------------------------------------------------------
public interface IModbusClient : IDisposable
{
    /// <summary>
    /// Open TCP connection to hardware-sim.
    /// Must be called before any read/write operations.
    /// </summary>
    Task ConnectAsync();

    /// <summary>
    /// FC16 — write motor command to holding registers 1000-1002.
    /// Motor speed is clamped to -1000..1000 RPM before writing.
    /// </summary>
    Task WriteMotorCommandAsync(short leftRpm, short rightRpm, CommandCode cmd);

    /// <summary>
    /// FC04 — read AGV state from input registers 2000-2007.
    /// </summary>
    Task<AgvState> ReadStatusAsync();

    /// <summary>True after ConnectAsync() succeeds.</summary>
    bool IsConnected { get; }
}

// ---------------------------------------------------------------------------
// Implementation
// ---------------------------------------------------------------------------
public class ModbusClient : IModbusClient
{
    private readonly string _host;
    private readonly int    _port;
    private readonly byte   _unitId;
    private readonly int    _timeoutMs;
    private readonly ILogger<ModbusClient> _logger;

    private TcpClient?     _tcpClient;
    private IModbusMaster? _master;
    private bool           _isConnected;

    // Reconnect delays: 1s → 2s → 5s (linear, not exponential — KISS)
    private static readonly int[] ReconnectDelaysMs = [1000, 2000, 5000];

    public bool IsConnected => _isConnected;

    public ModbusClient(IOptions<ModbusSettings> settings,
                        ILogger<ModbusClient> logger)
    {
        _host      = settings.Value.Host;
        _port      = settings.Value.Port;
        _unitId    = settings.Value.UnitId;
        _timeoutMs = settings.Value.TimeoutMs;
        _logger    = logger;
    }

    // -----------------------------------------------------------------------
    // ConnectAsync — open TCP socket and create NModbus master
    // -----------------------------------------------------------------------
    public async Task ConnectAsync()
    {
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(_host, _port);

        _tcpClient.ReceiveTimeout = _timeoutMs;
        _tcpClient.SendTimeout    = _timeoutMs;

        var factory = new ModbusFactory();
        _master = factory.CreateMaster(_tcpClient);
        _master.Transport.ReadTimeout  = _timeoutMs;
        _master.Transport.WriteTimeout = _timeoutMs;

        _isConnected = true;
        _logger.LogInformation("Connected to Modbus at {Host}:{Port}", _host, _port);
    }

    // -----------------------------------------------------------------------
    // WriteMotorCommandAsync — FC16 write holding registers 1000-1002
    // -----------------------------------------------------------------------
    public async Task WriteMotorCommandAsync(short leftRpm, short rightRpm, CommandCode cmd)
    {
        // Clamp to valid range before sending (defensive, C++ trusts these values)
        leftRpm  = Math.Clamp(leftRpm,  (short)-1000, (short)1000);
        rightRpm = Math.Clamp(rightRpm, (short)-1000, (short)1000);

        // Cast signed → unsigned for Modbus wire format (register stores uint16)
        ushort[] values = [(ushort)leftRpm, (ushort)rightRpm, (ushort)cmd];

        try
        {
            await Task.Run(() =>
                _master!.WriteMultipleRegisters(_unitId,
                                                ModbusRegisters.HoldingStart,
                                                values));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Modbus write failed: {Message}", ex.Message);
            _isConnected = false;
            await TryReconnectAsync();
            throw;   // Let Orchestrator know this cycle failed
        }
    }

    // -----------------------------------------------------------------------
    // ReadStatusAsync — FC04 read input registers 2000-2007 → AgvState
    // -----------------------------------------------------------------------
    public async Task<AgvState> ReadStatusAsync()
    {
        try
        {
            ushort[] regs = await Task.Run(() =>
                _master!.ReadInputRegisters(_unitId,
                                            ModbusRegisters.InputStart,
                                            ModbusRegisters.InputCount));

            return ParseRegisters(regs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Modbus read failed: {Message}", ex.Message);
            _isConnected = false;
            await TryReconnectAsync();
            throw;   // Let Orchestrator know this cycle failed
        }
    }

    // -----------------------------------------------------------------------
    // Internal static: parse raw Modbus registers → AgvState
    // Extracted for unit testability (Option C — no mock needed)
    // -----------------------------------------------------------------------
    internal static AgvState ParseRegisters(ushort[] regs) => new()
    {
        Status           = (StatusCode)regs[0],
        ActualLeftSpeed  = (short)regs[1],           // uint16 → int16 (signed cast)
        ActualRightSpeed = (short)regs[2],
        PositionX        = (short)regs[3],           // mm
        PositionY        = (short)regs[4],           // mm
        HeadingDegrees   = (regs[5] % 3600) / 10.0,  // 0-3599 → 0.0-359.9° (% guards 3600 edge)
        BatteryLevel     = regs[6],                   // 0-100 %
        Error            = (ErrorCode)regs[7],
        LastUpdated      = DateTime.UtcNow
    };

    // -----------------------------------------------------------------------
    // Dispose — release TCP connection on app shutdown
    // -----------------------------------------------------------------------
    public void Dispose()
    {
        _master = null;
        _tcpClient?.Dispose();
        _tcpClient   = null;
        _isConnected = false;
    }

    // -----------------------------------------------------------------------
    // Private: reconnect with linear backoff (1s → 2s → 5s)
    // -----------------------------------------------------------------------
    private async Task TryReconnectAsync()
    {
        foreach (int delayMs in ReconnectDelaysMs)
        {
            await Task.Delay(delayMs);
            try
            {
                await ConnectAsync();
                _logger.LogInformation("Reconnected to Modbus after {Delay}ms delay.", delayMs);
                return;   // Success — stop retrying
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Reconnect attempt failed ({Delay}ms): {Message}",
                                   delayMs, ex.Message);
            }
        }

        _logger.LogError("All Modbus reconnect attempts failed. IsConnected = false.");
    }
}
