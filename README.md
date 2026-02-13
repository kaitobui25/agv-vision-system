# AGV Vision Control System

> Industrial AGV control system integrating AI vision, path planning, and Modbus hardware communication

[![Python](https://img.shields.io/badge/Python-3.9+-blue.svg)](https://www.python.org/)
[![C#](https://img.shields.io/badge/C%23-.NET_8.0-purple.svg)](https://dotnet.microsoft.com/)
[![C++](https://img.shields.io/badge/C++-17-00599C.svg)](https://isocpp.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15+-336791.svg)](https://www.postgresql.org/)

---

## 🎯 System Overview

A complete AGV (Automated Guided Vehicle) warehouse automation system demonstrating:

- **AI Vision Processing** - YOLOv8 object detection for obstacle avoidance
- **Path Planning** - A* algorithm for optimal route calculation  
- **Industrial Communication** - Modbus TCP for motor control
- **Database Integration** - PostgreSQL for centralized logging

```
Camera → Vision AI → AGV Control → Hardware Controller → Motor Movement
                ↓         ↓              ↓
                    PostgreSQL Database
```

---

## 📦 Project Structure

```
agv-vision-system/
├── camera/              # Camera capture module (Python + OpenCV)
├── vision-ai/           # Object detection API (Python + FastAPI + YOLOv8)
├── agv-control/         # Path planning & control (C# .NET)
├── hardware-sim/        # Motor controller (C++ Modbus server)
├── database/            # PostgreSQL schema and migrations
├── docker/              # Docker Compose configuration
├── docs/                # Documentation
└── scripts/             # Utility scripts
```

---

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose
- Python 3.9+ (for local dev)
- .NET 8.0 SDK (for local dev)
- CMake & C++ compiler (for hardware sim)
- PostgreSQL 15+

### Run with Docker
```bash
docker-compose up
```

### Run Locally
See individual component READMEs:
- [camera/README.md](camera/README.md)
- [vision-ai/README.md](vision-ai/README.md)
- [agv-control/README.md](agv-control/README.md)
- [hardware-sim/README.md](hardware-sim/README.md)

---

## 🏗️ Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for detailed system design.

---

## 📝 License

MIT License - see [LICENSE](LICENSE) for details

---

**Built to demonstrate integration of AI vision, industrial protocols, and real-time control**
