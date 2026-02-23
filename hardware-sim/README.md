# Hardware Simulator

C++ Modbus TCP server simulating AGV motor controller.

## Build

```bash
cd hardware-sim
mkdir build && cd build
cmake ..
cmake --build .
```

## Run

```bash
./motor_controller
```

Listens on Modbus TCP port 502.

## Modbus Register Map

See [docs/04_MODBUS_REGISTER_MAP.md](../docs/04_MODBUS_REGISTER_MAP.md) for the complete register specification.

Summary:

- **Holding Registers** (1000-1002): Motor speeds (left/right) + command
- **Input Registers** (2000-2007): Status, actual speeds, position (x,y), heading, battery, error code
