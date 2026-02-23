// ==========================================================================
// ModbusRegisters.cs — Register addresses & enums from shared contract
// ==========================================================================
// Source of truth: docs/04_MODBUS_REGISTER_MAP.md
// Any change here → update the Modbus Register Map document too.
// ==========================================================================

namespace AgvControl.Models;

/// <summary>
/// Modbus register addresses for C# (agv-control) ↔ C++ (hardware-sim).
/// Matches docs/04_MODBUS_REGISTER_MAP.md exactly.
/// </summary>
public static class ModbusRegisters
{
    // -----------------------------------------------------------------------
    // Holding Registers (Read/Write) — C# writes commands, C++ reads
    // Function Code: FC03 (Read) / FC06 (Write Single) / FC16 (Write Multiple)
    // -----------------------------------------------------------------------
    public const ushort LeftMotorSpeed  = 1000;  // INT16, -1000→1000 RPM
    public const ushort RightMotorSpeed = 1001;  // INT16, -1000→1000 RPM
    public const ushort Command         = 1002;  // UINT16, CommandCode enum

    // -----------------------------------------------------------------------
    // Input Registers (Read Only) — C++ updates status, C# polls
    // Function Code: FC04 (Read Input Registers)
    // -----------------------------------------------------------------------
    public const ushort Status          = 2000;  // UINT16, StatusCode enum
    public const ushort ActualLeftSpeed = 2001;  // INT16, -1000→1000 RPM
    public const ushort ActualRightSpeed= 2002;  // INT16, -1000→1000 RPM
    public const ushort PositionX       = 2003;  // INT16, mm
    public const ushort PositionY       = 2004;  // INT16, mm
    public const ushort Heading         = 2005;  // UINT16, 0→3599 (0.1° units)
    public const ushort BatteryLevel    = 2006;  // UINT16, 0→100 %
    public const ushort ErrorCode       = 2007;  // UINT16, ErrorCode enum

    // -----------------------------------------------------------------------
    // Bulk read helpers
    // -----------------------------------------------------------------------

    /// <summary>First holding register address for bulk write.</summary>
    public const ushort HoldingStart = LeftMotorSpeed;  // 1000
    /// <summary>Number of holding registers to write (1000-1002).</summary>
    public const ushort HoldingCount = 3;

    /// <summary>First input register address for bulk read.</summary>
    public const ushort InputStart = Status;  // 2000
    /// <summary>Number of input registers to read (2000-2007).</summary>
    public const ushort InputCount = 8;

    // -----------------------------------------------------------------------
    // Physical Constants — from 04_MODBUS_REGISTER_MAP.md
    // -----------------------------------------------------------------------
    public const int WheelBaseMm    = 400;   // Distance between left/right wheels
    public const int WheelRadiusMm  = 50;    // Wheel radius
    public const int CameraOffsetMm = 300;   // Camera mounted 300mm ahead of rotation center
}

// ===========================================================================
// Command Codes (Holding Register 1002)
// ===========================================================================
public enum CommandCode : ushort
{
    /// <summary>No action — motors hold current state.</summary>
    Idle = 0,

    /// <summary>Execute motor speeds from registers 1000, 1001.</summary>
    Move = 1,

    /// <summary>Gradual stop — deceleration ramp over ~500ms.</summary>
    Stop = 2,

    /// <summary>Immediate stop — no ramp, cut power.</summary>
    EmergencyStop = 3,

    /// <summary>Clear error state, return to Idle.</summary>
    Reset = 4,
}

// ===========================================================================
// Status Codes (Input Register 2000)
// ===========================================================================
public enum StatusCode : ushort
{
    /// <summary>Ready for commands.</summary>
    Idle = 0,

    /// <summary>Motors are running.</summary>
    Moving = 1,

    /// <summary>Gradual stop completed.</summary>
    Stopped = 2,

    /// <summary>Emergency stop active.</summary>
    EStopped = 3,

    /// <summary>Fault condition — check ErrorCode register 2007.</summary>
    Error = 4,
}

// ===========================================================================
// Error Codes (Input Register 2007)
// ===========================================================================
public enum ErrorCode : ushort
{
    /// <summary>No error.</summary>
    Ok = 0,

    /// <summary>Motor current exceeded limit.</summary>
    MotorOverload = 1,

    /// <summary>Battery below 5%.</summary>
    BatteryCritical = 2,

    /// <summary>Position sensor malfunction.</summary>
    SensorFault = 3,

    /// <summary>No command received for 5 seconds.</summary>
    CommTimeout = 4,

    /// <summary>Motor blocked / cannot rotate.</summary>
    MotorStall = 5,
}
