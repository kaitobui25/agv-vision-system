// ==========================================================================
// AgvState.cs — Current AGV state from Modbus input registers
// ==========================================================================

namespace AgvControl.Models;

/// <summary>
/// Snapshot of AGV state read from Modbus input registers (2000-2007).
/// Updated every 100ms by AgvOrchestrator.
/// </summary>
public class AgvState
{
    /// <summary>Current X position in mm (register 2003).</summary>
    public int PositionX { get; set; }

    /// <summary>Current Y position in mm (register 2004).</summary>
    public int PositionY { get; set; }

    /// <summary>
    /// Heading in degrees (0.0 - 359.9°).
    /// Converted from register 2005: raw value / 10.0
    /// </summary>
    public double HeadingDegrees { get; set; }

    /// <summary>
    /// Heading in radians — used by Math.Cos/Sin for obstacle mapping.
    /// IMPORTANT: Modbus gives 0-3599 (0.1° units) → must convert to radians.
    /// </summary>
    public double HeadingRadians => HeadingDegrees * Math.PI / 180.0;

    /// <summary>Actual left motor speed in RPM (register 2001).</summary>
    public int ActualLeftSpeed { get; set; }

    /// <summary>Actual right motor speed in RPM (register 2002).</summary>
    public int ActualRightSpeed { get; set; }

    /// <summary>Battery percentage 0-100% (register 2006).</summary>
    public int BatteryLevel { get; set; }

    /// <summary>AGV status (register 2000).</summary>
    public StatusCode Status { get; set; }

    /// <summary>Error code (register 2007).</summary>
    public ErrorCode Error { get; set; }

    /// <summary>Timestamp when this state was last read from Modbus.</summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
