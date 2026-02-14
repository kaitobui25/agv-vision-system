# Camera Capture Module

Simple camera capture module using OpenCV for AGV vision system.

## Overview

Captures frames from USB webcam at 1 FPS and saves to `images/latest.jpg` for Vision AI processing.

## Features

- **Clean Architecture**: Separation of concerns (Capture vs Save)
- **SOLID Principles**: Single responsibility for each class
- **Auto-recovery**: Handles camera disconnection gracefully
- **Logging**: Detailed logs for debugging

## Setup

### Requirements

- Python 3.9+
- USB webcam (built-in laptop camera works)

### Installation

```bash
cd camera/
pip install -r requirements.txt
```

## Usage

### Basic Usage

```bash
python camera_server.py
```

This will:
1. Open default USB camera (index 0)
2. Capture frames every 1 second
3. Save to `images/latest.jpg`
4. Log each capture operation

### Output

```
2024-01-15 10:30:00 - camera - INFO - Opening camera 0...
2024-01-15 10:30:00 - camera - INFO - Camera opened: 640x480
2024-01-15 10:30:00 - camera - INFO - Output directory: /path/to/images
2024-01-15 10:30:00 - camera - INFO - Capture interval: 1.0s
2024-01-15 10:30:01 - camera - INFO - Saved: images/latest.jpg
2024-01-15 10:30:01 - camera - INFO - Frame #1 captured successfully
```

### Configuration

Edit these constants in `camera_server.py`:

```python
CAMERA_ID = 0           # USB camera index (0=default, 1=external)
OUTPUT_DIR = Path("images")
CAPTURE_INTERVAL = 1.0  # seconds between captures
IMAGE_WIDTH = 640
IMAGE_HEIGHT = 480
```

## Architecture

### Class Diagram

```
CameraCapture
  ├── open()           # Initialize camera
  ├── capture_frame()  # Get single frame
  └── close()          # Release resources

ImageSaver
  ├── save()           # Save with custom filename
  └── save_timestamped() # Save with timestamp
```

### Design Principles Applied

- **SRP**: `CameraCapture` only handles I/O, `ImageSaver` only handles storage
- **OCP**: Can extend with IP camera without modifying existing code
- **LSP**: `CameraCapture` can be substituted with `IPCameraCapture`
- **DRY**: Reusable components
- **KISS**: Simple loop, no overengineering

## Troubleshooting

### Camera not found

```
ERROR - Failed to open camera 0
```

**Solution**: 
- Check if camera is connected: `ls /dev/video*`
- Try different camera index: `CAMERA_ID = 1`
- Grant permissions: `sudo usermod -a -G video $USER`

### Permission denied

```
ERROR - Cannot access /dev/video0
```

**Solution**:
```bash
sudo chmod 666 /dev/video0
```

### Low FPS

If capture takes >1s, reduce resolution:
```python
IMAGE_WIDTH = 320
IMAGE_HEIGHT = 240
```

## Database Integration

### What Gets Logged

Camera module logs to `system_logs` table:

**Startup Event**:
```sql
INSERT INTO system_logs (level, component, event_type, message, details)
VALUES ('INFO', 'camera', 'startup', 'Camera server started', 
        '{"camera_id": 0, "resolution": "640x480"}');
```

**Milestone (every 100 frames)**:
```sql
INSERT INTO system_logs (level, component, event_type, message)
VALUES ('INFO', 'camera', 'capture_milestone', 'Camera milestone: 100 frames captured');
```

**Errors**:
```sql
INSERT INTO system_logs (level, component, event_type, message)
VALUES ('ERROR', 'camera', 'camera_open_failed', 'Failed to open camera - server cannot start');
```

### Setup Database

1. **Configure** `db_logger.py`:
```python
DB_CONFIG = {
    'host': 'localhost',
    'database': 'agv_control_db',
    'user': 'postgres',
    'password': 'your_password'  # CHANGE THIS!
}
```

2. **Install** PostgreSQL driver:
```bash
pip install psycopg2-binary
```

3. **Test** connection:
```bash
python db_logger.py
```

### Query Camera Events

```sql
-- Check camera health
SELECT timestamp, event_type, message 
FROM system_logs 
WHERE component = 'camera' 
ORDER BY timestamp DESC 
LIMIT 10;

-- Count capture failures
SELECT COUNT(*) FROM system_logs
WHERE component = 'camera' 
  AND event_type = 'capture_failure'
  AND timestamp >= CURRENT_DATE;
```

### Graceful Degradation

Camera works **without database**:
- If `db_logger.py` not found → Logs warning, continues
- If PostgreSQL unreachable → Logs to console only
- No database = no problem (for testing)

## Integration with Vision AI

The captured `images/latest.jpg` is read by Vision AI module:

```
camera_server.py  →  images/latest.jpg  →  vision-ai (POST /detect)
        ↓                                           ↓
   system_logs                                 detections
```

See `vision-ai/README.md` for API details.

## Testing

### Manual Test

```bash
# Terminal 1: Run camera
python camera_server.py

# Terminal 2: Check output
ls -lh images/latest.jpg
```

### Integration Test

```bash
# Run full system test
python test_integration.py
```

## Production Considerations

### For Real AGV Deployment

1. **Use IP Camera**: Replace OpenCV with RTSP stream
2. **Add Error Recovery**: Reconnect on camera failure
3. **Use Shared Memory**: Instead of file I/O (faster)
4. **Add Timestamps**: For synchronization with other sensors
5. **Implement Queue**: Buffer frames for batch processing

### Example: IP Camera

```python
# For RTSP camera
CAMERA_ID = "rtsp://admin:password@192.168.1.100/stream"
```

## License

MIT License - See root LICENSE file