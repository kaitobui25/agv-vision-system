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

- Register 100: Motor Speed (RPM)
- Register 101: Direction (0=Stop, 1=Forward, 2=Reverse)
- Register 102: Current Position (mm) [Read Only]
- Register 103: Battery Level (%) [Read Only]
- Register 104: Emergency Stop (0=Normal, 1=Stop)
