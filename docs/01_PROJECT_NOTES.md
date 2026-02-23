# Project Notes — AGV Vision Control System

> This file captures project context, decisions, and progress.  
> New conversations: read this file first to understand current state.

---

# 🇬🇧 English

## Purpose

Mini project to demonstrate skills for a job application. The target role requires:

- Image processing / Deep Learning / AI algorithm development
- AGV control software: communication, server/DB design, robot control logic
- Skills: C#, C++, Python, PostgreSQL, Docker

## Architecture Decision

5 modules, each using a different language to match JD requirements:

| Module          | Language                   | Purpose                                    |
| --------------- | -------------------------- | ------------------------------------------ |
| `camera/`       | Python + OpenCV            | Capture images                             |
| `vision-ai/`    | Python + FastAPI + YOLOv11 | Object detection API                       |
| `agv-control/`  | C# .NET 8.0                | Path planning (A\*), orchestration         |
| `hardware-sim/` | C++ + libmodbus            | Simulate AGV motor controller (Modbus TCP) |
| `database/`     | PostgreSQL                 | Centralized logging                        |

## Operational Flow

1. Camera captures image
   └─> Python + YOLO detects "Box ahead"
   └─> FastAPI returns to C#: {"obstacle": "box", "distance": "2m"}

2. C# (agv-control) receives result
   └─> Calculates: "Need to turn left"
   └─> Calls C++ DLL: TurnLeft(30°)

3. C++ (hardware-sim) receives command
   └─> libmodbus sends to Motor Controller: - Left motor: 300 RPM - Right motor: 500 RPM
   └─> AGV turns left successfully!

## Development Workflow

**Rule: Clean Code, SOLID, DRY, KISS, YAGNI, Naming Convention, Clean Architecture**

```
Step 1: database/init.sql       → test with psql
Step 2: vision-ai/ (Python)     → test with python app.py
Step 3: agv-control/ (C#)       → test with dotnet run
Step 4: hardware-sim/ (C++)     → test with cmake + run
Step 5: Dockerize               → docker-compose up
```

## Tool Split

- **Antigravity (this tool)**: Python code, SQL, Docker, architecture, review
- **Visual Studio 2022**: C# (.sln) and C++ (CMake) — better debugging, IntelliSense

Both tools work on the same folder. No conflict.

## DB Design — Business-Driven

Schema was designed by asking: "What questions does the business need to answer?"

### Case 1: Collision Investigation

> "AGV collided 3 times. Did the AI detect the obstacle?"

- Need: `detections` table (object_class, confidence, timestamp)
- Need: `system_logs` table (event type, timestamp, AGV speed)

### Case 2: Route Optimization

> "AGV takes 5 minutes for 20 meters. What route did it take?"

- Need: `paths` table (start/end point, waypoints, duration)

### Case 3: Daily Operations Report

> "How many trips yesterday? Any errors? Battery level?"

- Need: `paths` (count trips), `system_logs` (errors, battery)

## Key Technical Concepts Discussed

- **YOLO confidence & threshold**: Score 0-1 indicating detection certainty. Threshold too high → miss obstacles → dangerous. Too low → false detections → AGV stops constantly.
- **Multi-layer safety**: Real AGV uses YOLO (software) + LiDAR + bumper sensor + emergency stop. Never rely on AI alone.
- **Detection latency**: Camera → YOLO → Decision → Brake takes ~600ms. At 1m/s, AGV moves 60cm before stopping.

## Key Decisions (2026-02)

- **agv-control**: ASP.NET Web API (not console app) — provides REST endpoints for orchestration
- **Modbus Register Map**: Shared contract documented in `docs/04_MODBUS_REGISTER_MAP.md` — both C# and C++ follow same spec
- **Differential Drive**: 2 motors (left/right), holding registers 1000-1002, input registers 2000-2007
- **Grid Map**: Static warehouse layout 20x10m, cell size 500mm → grid 40x20. Static walls + dynamic obstacles from Vision AI
- **Control Loop**: BackgroundService polling every 100ms. agv-control polls Vision AI (GET `/detect/latest`)
- **API Endpoints**: `/health`, `/agv/start`, `/agv/stop`, `/agv/emergency-stop`, `/agv/status`, `/agv/map`
- **Vision → Grid Mapping**: Radian conversion (heading/10 × π/180), camera offset (+300mm), bounds check before grid access. Skip obstacle inflation (KISS)

## Current Progress

- [x] Project skeleton (folders, READMEs, .gitignore, LICENSE)
- [x] Architecture documentation
- [x] database/init.sql
- [x] camera/ (Python)
- [x] vision-ai/ (Python) — FastAPI + YOLOv11s detection API
- [x] Modbus Register Map — shared contract (docs/04_MODBUS_REGISTER_MAP.md)
- [ ] agv-control/ (C# ASP.NET Web API — in VS2022)
- [ ] hardware-sim/ (C++ — in VS2022)
- [ ] docker-compose.yml

---

# 🇯🇵 日本語

## プロジェクト概要

AGV（無人搬送車）ビジョン制御システムのミニプロジェクト。求人要件に合わせて作成：

- 画像処理・AIアルゴリズム開発
- AGV制御ソフトウェア：通信、サーバー/DB設計、ロボット制御ロジック
- 使用技術：C#、C++、Python、PostgreSQL、Docker

## アーキテクチャ

| モジュール      | 言語                       | 目的                                   |
| --------------- | -------------------------- | -------------------------------------- |
| `camera/`       | Python + OpenCV            | 画像キャプチャ                         |
| `vision-ai/`    | Python + FastAPI + YOLOv11 | 物体検出API                            |
| `agv-control/`  | C# .NET 8.0                | 経路計画（A\*）、統合制御              |
| `hardware-sim/` | C++ + libmodbus            | モーター制御シミュレータ（Modbus TCP） |
| `database/`     | PostgreSQL                 | 統合ログ管理                           |

## 動作フロー

1. カメラが画像を撮影
   └─> Python + YOLOが「前方の箱」を検出
   └─> FastAPIがC#に返却: {"obstacle": "box", "distance": "2m"}

2. C# (agv-control) が結果を受信
   └─> 計算: 「左折が必要」
   └─> C++ DLLを呼び出し: TurnLeft(30°)

3. C++ (hardware-sim) がコマンドを受信
   └─> libmodbusがモーターコントローラーに送信: - 左モーター: 300 RPM - 右モーター: 500 RPM
   └─> AGVが左折に成功!

## 開発方針

Clean Code, SOLID, DRY, KISS, YAGNI, Naming Convention, Clean Architecture

## 主要な決定事項 (2026-02)

- **agv-control**: ASP.NET Web API（コンソールアプリではない）
- **Modbus Register Map**: `docs/04_MODBUS_REGISTER_MAP.md` に共通仕様を文書化
- **差動駆動**: 2モーター（左/右）、Holding Registers 1000-1002、Input Registers 2000-2007
- **グリッドマップ**: 倉庫レイアウト 20x10m、セルサイズ 500mm → グリッド 40x20。静的壁 + Vision AIからの動的障害物
- **制御ループ**: BackgroundService、100msポーリング。agv-controlがVision AIをポーリング
- **APIエンドポイント**: `/health`, `/agv/start`, `/agv/stop`, `/agv/emergency-stop`, `/agv/status`, `/agv/map`
- **Vision→グリッド変換**: ラジアン変換、カメラオフセット(+300mm)、配列境界チェック。障害物膨張は省略(KISS)

## 現在の進捗

- [x] プロジェクトスケルトン (フォルダ、README、.gitignore、LICENSE)
- [x] アーキテクチャドキュメント
- [x] database/init.sql
- [x] camera/ (Python)
- [x] vision-ai/ (Python) — FastAPI + YOLOv11s 物体検出API
- [x] Modbus Register Map — 共通仕様 (docs/04_MODBUS_REGISTER_MAP.md)
- [ ] agv-control/ (C# ASP.NET Web API — VS2022)
- [ ] hardware-sim/ (C++ — VS2022)
- [ ] docker-compose.yml

---

# 🇻🇳 Tiếng Việt

## Mục tiêu

Mini project để show với nhà tuyển dụng. Yêu cầu công việc:

- Xử lý ảnh / Deep Learning / AI algorithm
- Phần mềm điều khiển AGV: giao tiếp, thiết kế server/DB, robot control logic
- Kỹ năng: C#, C++, Python, PostgreSQL, Docker

## Kiến trúc

| Module          | Ngôn ngữ                   | Mục đích                              |
| --------------- | -------------------------- | ------------------------------------- |
| `camera/`       | Python + OpenCV            | Chụp ảnh                              |
| `vision-ai/`    | Python + FastAPI + YOLOv11 | API phát hiện vật thể                 |
| `agv-control/`  | C# .NET 8.0                | Tìm đường (A\*), điều phối            |
| `hardware-sim/` | C++ + libmodbus            | Giả lập motor controller (Modbus TCP) |
| `database/`     | PostgreSQL                 | Lưu log tập trung                     |

## Luồng hoạt động HOÀN CHỈNH

1. Camera chụp ảnh
   └─> Python + YOLO phát hiện "Thùng hàng phía trước"
   └─> FastAPI trả về C#: {"obstacle": "box", "distance": 2m}

2. C# (agv-control) nhận kết quả
   └─> Tính toán: "Cần rẽ trái"
   └─> Gọi C++ DLL: TurnLeft(30°)

3. C++ (hardware-sim) nhận lệnh
   └─> libmodbus gửi đến Motor Controller: - Motor trái: 300 RPM - Motor phải: 500 RPM
   └─> AGV rẽ trái thành công! ✅

## Nguyên tắc phát triển

**Clean Code, SOLID, DRY, KISS, YAGNI, Naming Convention, Clean Architecture**

## Phân chia công cụ

- **Antigravity**: Code Python, SQL, Docker, review code
- **Visual Studio 2022**: Code C# và C++ (debug tốt hơn, IntelliSense)

Cả hai cùng mở chung 1 folder, không conflict.

## Thiết kế DB theo tư duy business

- **Case va chạm**: AI có detect không? → bảng `detections` (confidence, timestamp)
- **Case tối ưu đường đi**: Đi route nào, mấy phút? → bảng `paths` (waypoints, duration)
- **Case báo cáo**: Mấy chuyến, có lỗi gì? → bảng `system_logs`

## Quyết định quan trọng (2026-02)

- **agv-control**: ASP.NET Web API (không phải console app)
- **Modbus Register Map**: Tài liệu chung tại `docs/04_MODBUS_REGISTER_MAP.md` — cả C# và C++ code theo spec này
- **Differential Drive**: 2 motor (trái/phải), holding registers 1000-1002, input registers 2000-2007
- **Grid Map**: Warehouse tĩnh 20x10m, cell 500mm → grid 40x20. Tường cố định + obstacles động từ Vision AI
- **Control Loop**: BackgroundService polling 100ms. agv-control chủ động poll Vision AI (GET `/detect/latest`)
- **API Endpoints**: `/health`, `/agv/start`, `/agv/stop`, `/agv/emergency-stop`, `/agv/status`, `/agv/map`
- **Vision → Grid Mapping**: Đổi radian (heading/10 × π/180), cộng camera offset (+300mm), kiểm tra biên mảng. Bỏ qua obstacle inflation (KISS)

## Tiến độ hiện tại

- [x] Skeleton project (folders, README, .gitignore)
- [x] Tài liệu kiến trúc
- [x] database/init.sql
- [x] camera/ (Python)
- [x] vision-ai/ (Python) — FastAPI + YOLOv11s detection API
- [x] Modbus Register Map — tài liệu chung (docs/04_MODBUS_REGISTER_MAP.md)
- [ ] agv-control/ (C# ASP.NET Web API — code trong VS2022)
- [ ] hardware-sim/ (C++ — code trong VS2022)
- [ ] docker-compose.yml
