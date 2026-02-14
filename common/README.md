# Common Utilities - AGV Vision System

This folder contains **shared modules** used across multiple components of the AGV Vision System.

---

## 🔹 Key Module: `db_logger.py`

Provides **centralized database logging**:

| Logger | Purpose |
|--------|---------|
| `detection_logger` | Logs Vision AI detections (`detections` table) |
| `system_logger`    | Logs system events, errors, battery, trips (`system_logs` table) |

**Singleton usage** ensures one database connection across the entire project.

---

## 🗂️ Quick Usage

```python
from common.db_logger import detection_logger, system_logger

# Log a detection
detection_logger.log_detection(
    object_class='box',
    confidence=0.85,
    bbox={'x1':0.1, 'y1':0.2, 'x2':0.3, 'y2':0.4},
    distance_meters=2.5,
    triggered_stop=True
)

# Log a system event
system_logger.info(
    component='vision-ai',
    message='YOLO module started',
    event_type='startup',
    details={'model': 'yolo11n.pt'}
)
