# System Architecture

## Overview

The AGV Vision Control System consists of 5 main components working together:

```
┌─────────────┐
│   Camera    │ Captures warehouse images
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  Vision AI  │ Detects obstacles using YOLOv8
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ AGV Control │ Plans path with A* algorithm
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  Hardware   │ Executes motor commands via Modbus
└─────────────┘
       
       All components log to PostgreSQL
```

## Component Details

### 1. Camera Module
- **Language**: Python
- **Framework**: OpenCV
- **Purpose**: Capture images from USB webcam or IP camera
- **Output**: JPEG images

### 2. Vision AI
- **Language**: Python
- **Framework**: FastAPI + YOLOv8
- **Purpose**: Object detection for obstacle avoidance
- **Input**: Images
- **Output**: Bounding boxes, object classes, confidence scores

### 3. AGV Control Server
- **Language**: C#
- **Framework**: .NET 8.0
- **Purpose**: Path planning and orchestration
- **Algorithms**: A* pathfinding
- **Communication**: REST API (Vision), Modbus TCP (Hardware)

### 4. Hardware Simulator
- **Language**: C++
- **Library**: libmodbus
- **Purpose**: Simulate AGV motor controller
- **Protocol**: Modbus TCP (port 502)
- **Features**: Velocity ramping, position tracking, safety checks

### 5. PostgreSQL Database
- **Version**: 18+
- **Tables**: detections, paths, system_logs
- **Purpose**: Centralized logging and data storage

## Data Flow

1. Camera captures image every 1 second
2. Vision AI processes image, returns obstacles
3. AGV Control calls Vision API to get obstacle positions
4. AGV Control runs A* algorithm to plan path around obstacles
5. AGV Control sends motor commands via Modbus to Hardware
6. Hardware simulates motor movement and updates position
7. All actions logged to PostgreSQL

## Technology Stack

- **Python**: Vision AI, Camera
- **C#**: AGV Control
- **C++**: Hardware Simulator
- **PostgreSQL**: Database
- **Docker**: Containerization
- **Modbus TCP**: Industrial communication protocol
