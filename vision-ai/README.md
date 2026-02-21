# Vision AI Module

YOLOv11s-based object detection API for AGV warehouse automation.

## Overview

FastAPI server that accepts images and returns detected objects (bounding boxes, classes, confidence scores). Integrates with `common/db_logger.py` to log all detections to PostgreSQL.

```
camera/images/latest.jpg → POST /detect → YOLOv11s inference → JSON response
                                                  ↓
                                    detections table (PostgreSQL)
```

## Features

- **YOLOv11s Detection**: Small model (~22MB), good balance of speed and accuracy
- **Clean Architecture**: `YoloDetector` class handles ONLY inference (SRP)
- **Graceful Degradation**: Works without PostgreSQL — logs warning, continues
- **Adjustable Threshold**: Query parameter `?threshold=0.7` per request
- **Auto-detect Latest**: `GET /detect/latest` reads directly from camera output

## Setup

### Requirements

- Python 3.14+
- USB webcam or test images

### Installation

```bash
cd vision-ai/
pip install -r requirements.txt
```

> **Note**: First run will auto-download `yolo11s.pt` (~22MB) from Ultralytics hub.

## Usage

### Start Server

```bash
python app.py
```

Server runs at `http://localhost:8000`

### API Endpoints

| Method | Endpoint         | Description                       |
| ------ | ---------------- | --------------------------------- |
| `GET`  | `/health`        | Health check + model status       |
| `POST` | `/detect`        | Detect objects in uploaded image  |
| `GET`  | `/detect/latest` | Detect from camera's latest frame |

### Interactive API Docs

Open `http://localhost:8000/docs` for Swagger UI with live testing.

### Example: Detect Objects

```bash
# Upload an image
curl -X POST http://localhost:8000/detect \
  -F "file=@camera/images/latest.jpg"
```

Response:

```json
{
    "detections": [
        {
            "object_class": "person",
            "confidence": 0.8723,
            "bbox": { "x1": 0.12, "y1": 0.34, "x2": 0.56, "y2": 0.78 },
            "bbox_pixels": { "x1": 77, "y1": 163, "x2": 358, "y2": 374 },
            "distance_meters": null
        }
    ],
    "processing_time_ms": 45,
    "total_objects": 1
}
```

### Example: Detect from Camera

```bash
# Auto-reads camera/images/latest.jpg
curl http://localhost:8000/detect/latest
```

### Example: Custom Threshold

```bash
# Higher threshold = fewer false detections
curl http://localhost:8000/detect/latest?threshold=0.7
```

### Example: Health Check

```bash
curl http://localhost:8000/health
```

Response:

```json
{
    "status": "ok",
    "model": "yolo11s.pt",
    "db_connected": true
}
```

## Architecture

### Class Diagram

```
YoloDetector
  ├── __init__(model_name)  # Load YOLO model once
  └── detect(image, threshold) → dict  # Run inference

FastAPI Endpoints
  ├── GET  /health         # Status check
  ├── POST /detect         # Upload image → detect
  └── GET  /detect/latest  # Read camera output → detect
```

### Design Principles Applied

- **SRP**: `YoloDetector` handles only inference, API routing is separate
- **OCP**: Can swap YOLO for another model without changing endpoints
- **DRY**: Detection logic in one place, reused by both endpoints
- **KISS**: Simple endpoints, no overengineering
- **Graceful Degradation**: DB optional — camera and detection still work

## Database Integration

### What Gets Logged

Vision AI logs to two tables:

**`detections` table** (per detected object):

```sql
INSERT INTO detections (object_class, confidence, bbox_x1, bbox_y1, bbox_x2, bbox_y2,
                        processing_time_ms, image_path, triggered_stop)
VALUES ('person', 0.87, 0.12, 0.34, 0.56, 0.78, 45, 'latest.jpg', false);
```

**`system_logs` table** (startup/shutdown events):

```sql
INSERT INTO system_logs (level, component, event_type, message, details)
VALUES ('INFO', 'vision-ai', 'startup', 'Vision AI server started',
        '{"model": "yolo11s.pt", "threshold": 0.5}');
```

### Without Database

If PostgreSQL is not running, the server still works:

- Loads model normally
- Returns detection results normally
- Logs warning: "running without database logging"

## Configuration

Edit constants in `app.py`:

```python
MODEL_NAME = "yolo11s.pt"              # YOLO model (s=small, n=nano, m=medium)
DEFAULT_CONFIDENCE_THRESHOLD = 0.5     # Default threshold
CAMERA_IMAGE_PATH = "../camera/images/latest.jpg"  # Camera output
```

## Troubleshooting

### Model download fails

```
ERROR - Failed to load YOLO model
```

**Solution**: Download manually from [Ultralytics](https://docs.ultralytics.com/models/yolo11/) and place in `vision-ai/` directory.

### Out of memory (GPU)

**Solution**: Use CPU-only mode — `ultralytics` auto-falls back to CPU if GPU unavailable.

### Slow inference

- Check if running on CPU vs GPU: GPU is ~10x faster
- Use smaller model: change `MODEL_NAME = "yolo11n.pt"` (nano)
- Reduce image resolution before sending

## Integration with Other Modules

```
camera_server.py  →  images/latest.jpg  →  vision-ai POST /detect
                                                   ↓
                                             JSON response
                                                   ↓
                                         agv-control (C#)
                                                   ↓
                                        hardware-sim TurnLeft(30°)
```

## License

MIT License - See root LICENSE file
