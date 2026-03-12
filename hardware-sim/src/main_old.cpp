// ==========================================================================
// main.cpp — AGV Hardware Simulator (Modbus TCP Server)
// ==========================================================================
// Simulates a differential-drive AGV with Modbus TCP interface.
// Contract: docs/04_MODBUS_REGISTER_MAP.md
//
// Build: Visual Studio 2022 (CMake project with vcpkg + libmodbus)
// ==========================================================================

#ifdef _WIN32
#include <winsock2.h>   // Must come before windows.h — needed for closesocket, select
#endif

#include <cstdio>
#include <cstdint>
#include <cmath>
#include <csignal>
#include <cerrno>
#include <algorithm>
#include <chrono>
#include <thread>

#include <modbus.h>

// EAGAIN is not defined on MSVC — use EWOULDBLOCK as fallback
#ifndef EAGAIN
#define EAGAIN EWOULDBLOCK
#endif

#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

// ==========================================================================
// Block 1: Constants & Enums
// ==========================================================================

// ---- Register Addresses (mirrors C# ModbusRegisters.cs) ----
constexpr int HOLDING_START = 1000;   // left_motor_speed
constexpr int HOLDING_COUNT = 3;      // 1000, 1001, 1002
constexpr int INPUT_START   = 2000;   // status
constexpr int INPUT_COUNT   = 8;      // 2000-2007

// ---- Physical Constants (from 04_MODBUS_REGISTER_MAP.md) ----
constexpr double WHEEL_BASE_MM   = 400.0;
constexpr double WHEEL_RADIUS_MM = 50.0;

// ---- Timing ----
constexpr int    SIM_TICK_MS        = 50;     // 50ms = 20 Hz
constexpr double SIM_TICK_S         = SIM_TICK_MS / 1000.0;
constexpr double WATCHDOG_TIMEOUT_S = 5.0;
constexpr double RAMP_DURATION_S    = 0.5;    // 500ms deceleration ramp
constexpr int    LOG_INTERVAL_TICKS = 20;     // Log every 1s (20 * 50ms)

// ---- Command Codes (Holding Register 1002) ----
enum Command : uint16_t {
    CMD_IDLE           = 0,
    CMD_MOVE           = 1,
    CMD_STOP           = 2,
    CMD_EMERGENCY_STOP = 3,
    CMD_RESET          = 4
};

// ---- Status Codes (Input Register 2000) ----
enum Status : uint16_t {
    ST_IDLE     = 0,
    ST_MOVING   = 1,
    ST_STOPPED  = 2,
    ST_ESTOPPED = 3,
    ST_ERROR    = 4
};

// ---- Error Codes (Input Register 2007) ----
enum Error : uint16_t {
    ERR_OK               = 0,
    ERR_MOTOR_OVERLOAD   = 1,
    ERR_BATTERY_CRITICAL = 2,
    ERR_SENSOR_FAULT     = 3,
    ERR_COMM_TIMEOUT     = 4,
    ERR_MOTOR_STALL      = 5
};

// ==========================================================================
// Block 2: Global State
// ==========================================================================

// High-precision simulation state (double, not register-limited)
double pos_x   = 0.0;   // mm
double pos_y   = 0.0;   // mm
double heading = 0.0;   // radians

// Actual motor speeds (with deceleration ramp)
double actual_left  = 0.0;  // RPM
double actual_right = 0.0;  // RPM

// Current status and error
Status current_status = ST_IDLE;
Error  current_error  = ERR_OK;

// Watchdog
auto last_cmd_time = std::chrono::steady_clock::now();

// Graceful shutdown flag
volatile bool running = true;

// ==========================================================================
// Block 3: Helper Functions
// ==========================================================================

// Signal handler for Ctrl+C
void signal_handler(int /*signum*/) {
    printf("\n[hardware-sim] Shutting down...\n");
    running = false;
}

// Convert heading (radians) to 0-3599 (0.1 degree units)
uint16_t heading_to_decideg(double rad) {
    double deg = std::fmod(rad * 180.0 / M_PI, 360.0);
    if (deg < 0.0) deg += 360.0;
    return static_cast<uint16_t>(deg * 10.0);
}

// Clamp double to int16_t range
int16_t clamp_to_int16(double val) {
    val = std::clamp(val, -32768.0, 32767.0);
    return static_cast<int16_t>(val);
}

// Apply deceleration ramp toward zero
double decelerate(double current_speed, double dt) {
    if (std::abs(current_speed) < 1.0) return 0.0;
    double decay = 1.0 - (dt / RAMP_DURATION_S);
    if (decay < 0.0) decay = 0.0;
    return current_speed * decay;
}

// Main simulation step — called every SIM_TICK_MS
void simulation_tick(modbus_mapping_t* map) {
    // Read command from holding registers
    auto cmd = static_cast<Command>(map->tab_registers[2]);       // reg 1002
    int16_t cmd_left  = static_cast<int16_t>(map->tab_registers[0]);  // reg 1000
    int16_t cmd_right = static_cast<int16_t>(map->tab_registers[1]);  // reg 1001

    // State machine
    switch (cmd) {
    case CMD_MOVE:
        if (current_status == ST_ESTOPPED || current_status == ST_ERROR) {
            // Cannot move while in error/e-stop state
            break;
        }
        actual_left  = static_cast<double>(cmd_left);
        actual_right = static_cast<double>(cmd_right);
        current_status = ST_MOVING;
        break;

    case CMD_STOP:
        actual_left  = decelerate(actual_left, SIM_TICK_S);
        actual_right = decelerate(actual_right, SIM_TICK_S);
        if (std::abs(actual_left) < 1.0 && std::abs(actual_right) < 1.0) {
            actual_left = actual_right = 0.0;
            current_status = ST_STOPPED;
        } else {
            current_status = ST_MOVING;  // Still decelerating
        }
        break;

    case CMD_EMERGENCY_STOP:
        actual_left  = 0.0;
        actual_right = 0.0;
        current_status = ST_ESTOPPED;
        break;

    case CMD_RESET:
        current_error  = ERR_OK;
        current_status = ST_IDLE;
        actual_left  = 0.0;
        actual_right = 0.0;
        break;

    case CMD_IDLE:
    default:
        // Hold current state — if moving, keep moving
        if (current_status == ST_MOVING) {
            // Continue with deceleration if no new MOVE command
            actual_left  = decelerate(actual_left, SIM_TICK_S);
            actual_right = decelerate(actual_right, SIM_TICK_S);
            if (std::abs(actual_left) < 1.0 && std::abs(actual_right) < 1.0) {
                actual_left = actual_right = 0.0;
                current_status = ST_IDLE;
            }
        }
        break;
    }

    // Differential drive kinematics (only when motors are spinning)
    if (std::abs(actual_left) > 0.0 || std::abs(actual_right) > 0.0) {
        // RPM to mm/s
        double v_left  = actual_left  * WHEEL_RADIUS_MM * 2.0 * M_PI / 60.0;
        double v_right = actual_right * WHEEL_RADIUS_MM * 2.0 * M_PI / 60.0;

        double v_linear  = (v_left + v_right) / 2.0;           // mm/s
        double v_angular = (v_right - v_left) / WHEEL_BASE_MM; // rad/s

        pos_x   += v_linear * std::cos(heading) * SIM_TICK_S;
        pos_y   += v_linear * std::sin(heading) * SIM_TICK_S;
        heading += v_angular * SIM_TICK_S;
    }

    // Write results to input registers
    map->tab_input_registers[0] = static_cast<uint16_t>(current_status);    // 2000: status
    map->tab_input_registers[1] = static_cast<uint16_t>(clamp_to_int16(actual_left));   // 2001
    map->tab_input_registers[2] = static_cast<uint16_t>(clamp_to_int16(actual_right));  // 2002
    map->tab_input_registers[3] = static_cast<uint16_t>(clamp_to_int16(pos_x));         // 2003
    map->tab_input_registers[4] = static_cast<uint16_t>(clamp_to_int16(pos_y));         // 2004
    map->tab_input_registers[5] = heading_to_decideg(heading);               // 2005: heading
    map->tab_input_registers[6] = 100;                                       // 2006: battery (fixed)
    map->tab_input_registers[7] = static_cast<uint16_t>(current_error);      // 2007: error
}

// Watchdog: e-stop if no command received for WATCHDOG_TIMEOUT_S
void watchdog_check(modbus_mapping_t* map) {
    auto elapsed = std::chrono::steady_clock::now() - last_cmd_time;
    double elapsed_s = std::chrono::duration<double>(elapsed).count();

    if (elapsed_s > WATCHDOG_TIMEOUT_S && current_status != ST_ESTOPPED) {
        printf("[SIM] WATCHDOG: No command for %.0fs — emergency stop!\n", elapsed_s);
        actual_left  = 0.0;
        actual_right = 0.0;
        current_status = ST_ESTOPPED;
        current_error  = ERR_COMM_TIMEOUT;

        // Update input registers immediately
        map->tab_input_registers[0] = ST_ESTOPPED;
        map->tab_input_registers[1] = 0;
        map->tab_input_registers[2] = 0;
        map->tab_input_registers[7] = ERR_COMM_TIMEOUT;
    }
}

// ==========================================================================
// Block 4: main()
// ==========================================================================

int main() {
    // Register signal handler for graceful shutdown
    std::signal(SIGINT, signal_handler);
    std::signal(SIGTERM, signal_handler);

    printf("[hardware-sim] AGV Hardware Simulator (Modbus TCP Server)\n");
    printf("[hardware-sim] Register map: docs/04_MODBUS_REGISTER_MAP.md\n");
    printf("==========================================================\n");

    // ---- 1. Create Modbus TCP context ----
    //modbus_t* ctx = modbus_new_tcp("0.0.0.0", MODBUS_TCP_DEFAULT_PORT);

    //========================debug
    modbus_t* ctx = modbus_new_tcp("0.0.0.0", 1502);



    if (ctx == nullptr) {
        printf("[hardware-sim] ERROR: Failed to create Modbus context: %s\n",
               modbus_strerror(errno));
        return 1;
    }

    // ---- 2. Allocate register memory ----
    // Param order: start_bits, nb_bits, start_input_bits, nb_input_bits,
    //              start_registers, nb_registers, start_input_registers, nb_input_registers
    modbus_mapping_t* mb_mapping = modbus_mapping_new_start_address(
        0, 0,                          // Coils: not used
        0, 0,                          // Discrete inputs: not used
        HOLDING_START, HOLDING_COUNT,  // Holding registers: 1000-1002
        INPUT_START,   INPUT_COUNT     // Input registers: 2000-2007
    );

    if (mb_mapping == nullptr) {
        printf("[hardware-sim] ERROR: Failed to allocate registers: %s\n",
               modbus_strerror(errno));
        modbus_free(ctx);
        return 1;
    }

    // Initialize input registers to default values
    mb_mapping->tab_input_registers[0] = ST_IDLE;   // status
    mb_mapping->tab_input_registers[6] = 100;       // battery = 100%

    // ---- 3. Start listening ----
    int server_socket = modbus_tcp_listen(ctx, 1);
    if (server_socket == -1) {
        printf("[hardware-sim] ERROR: Failed to listen on port %d: %s\n",
               MODBUS_TCP_DEFAULT_PORT, modbus_strerror(errno));
        modbus_mapping_free(mb_mapping);
        modbus_free(ctx);
        return 1;
    }

    printf("[hardware-sim] Listening on port %d\n", MODBUS_TCP_DEFAULT_PORT);

    // Set a short indication timeout so modbus_receive() doesn't block forever
    // This allows the simulation loop to run even when no client data arrives
    modbus_set_indication_timeout(ctx, 0, SIM_TICK_MS * 1000);  // microseconds

    // ---- 4. Main loop ----
    uint8_t query[MODBUS_TCP_MAX_ADU_LENGTH];
    int tick_counter = 0;

    while (running) {
        // Wait for client connection
        printf("[hardware-sim] Waiting for client connection...\n");
        int client_socket = -1;
        
        while (running && client_socket == -1) {
            // Use a short timeout so we can check 'running' flag
            fd_set rfds;
            FD_ZERO(&rfds);
            FD_SET(server_socket, &rfds);

            struct timeval tv;
            tv.tv_sec = 1;
            tv.tv_usec = 0;

            int rc = select(server_socket + 1, &rfds, nullptr, nullptr, &tv);
            if (rc > 0) {
                client_socket = -1;
                if (modbus_tcp_accept(ctx, &client_socket) == -1) {
                    printf("[hardware-sim] ERROR: Accept failed: %s\n",
                           modbus_strerror(errno));
                    client_socket = -1;
                }
            }
        }

        if (!running) break;

        printf("[hardware-sim] Client connected!\n");
        last_cmd_time = std::chrono::steady_clock::now();
        tick_counter = 0;

        // Client session loop
        bool client_connected = true;
        while (running && client_connected) {
            auto tick_start = std::chrono::steady_clock::now();

            // Try to receive a Modbus request (non-blocking via indication timeout)
            int rc = modbus_receive(ctx, query);
            if (rc > 0) {
                // Valid request received — reply and reset watchdog
                modbus_reply(ctx, query, rc, mb_mapping);
                last_cmd_time = std::chrono::steady_clock::now();
            } else if (rc == -1) {
                // Check if it's a real error or just timeout (no data)
                if (errno != ETIMEDOUT && errno != EAGAIN
#ifdef _WIN32
                    && errno != WSAETIMEDOUT && errno != WSAEWOULDBLOCK
#endif
                ) {
                    printf("[hardware-sim] Client disconnected: %s\n",
                           modbus_strerror(errno));
                    client_connected = false;
                    continue;
                }
                // Timeout — no data, that's fine, continue simulation
            }

            // Run simulation and watchdog every tick
            simulation_tick(mb_mapping);
            watchdog_check(mb_mapping);

            // Periodic logging (every ~1 second)
            tick_counter++;
            if (tick_counter >= LOG_INTERVAL_TICKS) {
                tick_counter = 0;
                double heading_deg = std::fmod(heading * 180.0 / M_PI, 360.0);
                if (heading_deg < 0.0) heading_deg += 360.0;

                printf("[SIM] status=%u  pos=(%.0f, %.0f)  heading=%.1f deg  "
                       "motors=(%+.0f, %+.0f)  cmd=%u  err=%u\n",
                       current_status, pos_x, pos_y, heading_deg,
                       actual_left, actual_right,
                       mb_mapping->tab_registers[2], current_error);
            }

            // Sleep to maintain tick rate
            auto tick_end = std::chrono::steady_clock::now();
            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                tick_end - tick_start);
            int sleep_ms = SIM_TICK_MS - static_cast<int>(elapsed.count());
            if (sleep_ms > 0) {
                std::this_thread::sleep_for(std::chrono::milliseconds(sleep_ms));
            }
        }

        printf("[hardware-sim] Session ended. Waiting for reconnection...\n");
    }

    // ---- 5. Cleanup ----
    printf("[hardware-sim] Cleaning up...\n");

#ifdef _WIN32
    closesocket(server_socket);
#else
    close(server_socket);
#endif

    modbus_mapping_free(mb_mapping);
    modbus_close(ctx);
    modbus_free(ctx);

    printf("[hardware-sim] Goodbye!\n");
    return 0;
}