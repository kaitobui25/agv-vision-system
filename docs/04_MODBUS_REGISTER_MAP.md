# Modbus Register Map — Shared Contract

> C# (agv-control) ↔ C++ (hardware-sim) communication spec.
> Both sides MUST follow this document. Any change here → update both codebases.

---

## Overview

| Item              | Value                           |
| ----------------- | ------------------------------- |
| Protocol          | Modbus TCP                      |
| Server (Slave)    | C++ hardware-sim (port 502)     |
| Client (Master)   | C# agv-control                  |
| Byte Order        | Big-Endian (Modbus standard)    |
| Unit ID           | 1                               |
| Polling Interval  | 100ms (agv-control → hardware)  |
| Timeout           | 1000ms (if no response → error) |

### Drive Type

**Differential drive** — 2 independent motors (left / right).

- Same speed, same direction → go straight
- Different speed → turn
- Opposite direction → spin in place

### Physical Constants

| Constant         | Value  | Unit | Description                                      |
| ---------------- | ------ | ---- | ------------------------------------------------ |
| wheel_base       | 400    | mm   | Distance between left and right wheels            |
| wheel_radius     | 50     | mm   | Wheel radius                                      |
| camera_offset    | 300    | mm   | Distance from rotation center to camera (front)   |

> **Camera offset**: Camera is mounted at front bumper, 300mm ahead of the wheel axis (center of rotation).
> When Vision AI reports `distance = 2000mm`, actual distance from AGV center = `2000 + 300 = 2300mm`.

---

## Holding Registers (Read/Write)

> **Function Code**: FC03 (Read) / FC06 (Write Single) / FC16 (Write Multiple)
>
> C# writes commands → C++ reads and executes.

| Address | Name               | Type   | Range          | Unit | Description                              |
| ------- | ------------------ | ------ | -------------- | ---- | ---------------------------------------- |
| 1000    | left_motor_speed   | INT16  | -1000 → 1000   | RPM  | Left motor speed (negative = reverse)    |
| 1001    | right_motor_speed  | INT16  | -1000 → 1000   | RPM  | Right motor speed (negative = reverse)   |
| 1002    | command            | UINT16 | 0 → 4          | -    | Command code (see table below)           |

### Command Codes (Register 1002)

| Value | Name           | Description                                    |
| ----- | -------------- | ---------------------------------------------- |
| 0     | IDLE           | No action — motors hold current state          |
| 1     | MOVE           | Execute motor speeds from register 1000, 1001  |
| 2     | STOP           | Gradual stop (deceleration ramp)               |
| 3     | EMERGENCY_STOP | Immediate stop — no ramp, cut power            |
| 4     | RESET          | Clear error state, return to IDLE              |

---

## Input Registers (Read Only)

> **Function Code**: FC04 (Read Input Registers)
>
> C++ updates status → C# polls to read.

| Address | Name               | Type   | Range          | Unit | Description                      |
| ------- | ------------------ | ------ | -------------- | ---- | -------------------------------- |
| 2000    | status             | UINT16 | 0 → 4          | -    | AGV status (see table below)     |
| 2001    | actual_left_speed  | INT16  | -1000 → 1000   | RPM  | Actual left motor speed          |
| 2002    | actual_right_speed | INT16  | -1000 → 1000   | RPM  | Actual right motor speed         |
| 2003    | position_x         | INT16  | -32768 → 32767 | mm   | Current X position               |
| 2004    | position_y         | INT16  | -32768 → 32767 | mm   | Current Y position               |
| 2005    | heading            | UINT16 | 0 → 3599       | 0.1° | Heading angle (0 → 359.9°)      |
| 2006    | battery_level      | UINT16 | 0 → 100        | %    | Battery percentage               |
| 2007    | error_code         | UINT16 | 0 → 255        | -    | Error code (see table below)     |

### Status Codes (Register 2000)

| Value | Name        | Description                                |
| ----- | ----------- | ------------------------------------------ |
| 0     | IDLE        | Ready for commands                         |
| 1     | MOVING      | Motors are running                         |
| 2     | STOPPED     | Gradual stop completed                     |
| 3     | E_STOPPED   | Emergency stop active                      |
| 4     | ERROR       | Fault condition — check error_code (2007)  |

### Error Codes (Register 2007)

| Value | Name              | Description                          |
| ----- | ----------------- | ------------------------------------ |
| 0     | OK                | No error                             |
| 1     | MOTOR_OVERLOAD    | Motor current exceeded limit         |
| 2     | BATTERY_CRITICAL  | Battery below 5%                     |
| 3     | SENSOR_FAULT      | Position sensor malfunction          |
| 4     | COMM_TIMEOUT      | No command received for 5 seconds    |
| 5     | MOTOR_STALL       | Motor blocked / cannot rotate        |

---

## Communication Flow

```
┌──────────────────┐                          ┌──────────────────┐
│  C# agv-control  │                          │  C++ hardware-sim│
│  (Modbus Client) │                          │  (Modbus Server) │
└────────┬─────────┘                          └────────┬─────────┘
         │                                             │
         │  1. Write holding registers                 │
         │     [1000]=500, [1001]=500, [1002]=1(MOVE)  │
         │ ─────────────────────────────────────────►  │
         │                                             │
         │                          2. C++ reads command
         │                             Accelerates motors
         │                             Updates position (x,y)
         │                                             │
         │  3. Poll input registers (every 100ms)      │
         │ ─────────────────────────────────────────►  │
         │                                             │
         │  4. Response: [2000]=1(MOVING),             │
         │     [2001]=480, [2002]=480,                 │
         │     [2003]=1500, [2004]=800, ...            │
         │ ◄─────────────────────────────────────────  │
         │                                             │
         │  5. Obstacle detected! Write STOP           │
         │     [1002]=2(STOP)                          │
         │ ─────────────────────────────────────────►  │
         │                                             │
         │                          6. C++ decelerates
         │                             Updates status=STOPPED
         │                                             │
```

## Turning Examples

| Action            | Left RPM | Right RPM | Result                      |
| ----------------- | -------- | --------- | --------------------------- |
| Go straight       | 500      | 500       | Forward at 500 RPM          |
| Turn left (soft)  | 300      | 500       | Curves left                 |
| Turn right (soft) | 500      | 300       | Curves right                |
| Spin left         | -300     | 300       | Rotates counter-clockwise   |
| Spin right        | 300      | -300      | Rotates clockwise           |
| Reverse           | -500     | -500      | Backward at 500 RPM         |

---

## Position Tracking (C++ Simulation)

C++ simulates position based on motor speeds using differential drive kinematics:

```
wheel_base   = 400 mm  (distance between left and right wheels)
wheel_radius = 50 mm

v_left  = left_speed  * wheel_radius * 2π / 60   (mm/s)
v_right = right_speed * wheel_radius * 2π / 60   (mm/s)

v_linear  = (v_left + v_right) / 2                (mm/s — forward speed)
v_angular = (v_right - v_left) / wheel_base        (rad/s — rotation speed)

Δx = v_linear * cos(heading) * Δt
Δy = v_linear * sin(heading) * Δt
Δheading = v_angular * Δt
```

---

## Safety Rules

1. **Emergency Stop priority** — EMERGENCY_STOP (command=3) overrides everything, instant motor cut
2. **Timeout watchdog** — If C++ receives no command for 5 seconds → auto-stop, set error_code=4
3. **Battery protection** — If battery < 5% → refuse MOVE, set error_code=2
4. **Speed ramp** — STOP (command=2) decelerates from current speed to 0 over ~500ms

---

## Implementation Checklist

- [ ] C++ — Create Modbus TCP server listening on port 502
- [ ] C++ — Implement holding register read/write handler
- [ ] C++ — Implement input register update loop (position simulation)
- [ ] C++ — Implement safety watchdog (5s timeout)
- [ ] C# — Create Modbus TCP client connecting to localhost:502
- [ ] C# — Write motor commands to holding registers
- [ ] C# — Poll input registers every 100ms
- [ ] C# — Handle error codes and status changes
