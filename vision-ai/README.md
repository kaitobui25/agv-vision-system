# Vision AI Module

YOLOv8-based object detection API for warehouse automation.

## Setup

```bash
cd vision-ai
pip install -r requirements.txt
```

## Run

```bash
python app.py
```

API will be available at `http://localhost:8000`

## Endpoints

- `POST /detect` - Detect objects in uploaded image
- `GET /health` - Health check
