
## 🏗️ Kiến trúc đã implement:

```
Camera (USB Webcam) 
  ↓ 1 FPS capture
images/latest.jpg
  ↓ read by
Vision AI (FastAPI:8000)
  ↓ YOLOv11 inference (45-60ms)
JSON: {"obstacle": "person", "confidence": 0.89, "distance": 3.2m}
  ↓ next step
AGV Control (C#) ← STEP 3
```


## ⏭️ Next Step: 
