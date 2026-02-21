"""
Vision AI Module — Object Detection API
========================================
FastAPI server using YOLOv11s for object detection in AGV warehouse system.

Clean Architecture:
- Single Responsibility: YoloDetector handles ONLY model inference
- Separation of Concerns: API routing separate from detection logic
- Graceful Degradation: Works without database connection

Integration:
- Reads images from camera/images/latest.jpg (or uploaded file)
- Logs detections to PostgreSQL via common/db_logger.py
- Returns JSON for agv-control (C#) to consume
"""

import sys
import time
import logging
from pathlib import Path
from typing import Optional
from contextlib import asynccontextmanager

import cv2
import numpy as np
from fastapi import FastAPI, File, UploadFile, Query, HTTPException
from fastapi.responses import JSONResponse
from ultralytics import YOLO

# ---------------------------------------------------------------------------
# Path Setup — allow importing common/ from project root
# ---------------------------------------------------------------------------
PROJECT_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT_ROOT))

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------
MODEL_NAME = "yolo11s.pt"  # Small model (~22MB), good balance speed/accuracy
DEFAULT_CONFIDENCE_THRESHOLD = 0.5
CAMERA_IMAGE_PATH = PROJECT_ROOT / "camera" / "images" / "latest.jpg"

# Logging setup
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger("vision-ai")

# ---------------------------------------------------------------------------
# Database Logger (optional — graceful degradation)
# ---------------------------------------------------------------------------
try:
    from common.db_logger import detection_logger, system_logger
    DB_AVAILABLE = True
    logger.info("Database logger loaded — detections will be logged to PostgreSQL")
except ImportError:
    DB_AVAILABLE = False
    logger.warning("common.db_logger not found — running without database logging")


# ===========================================================================
# YoloDetector — Single Responsibility: Model inference ONLY
# ===========================================================================
class YoloDetector:
    """
    YOLO object detection wrapper.

    SOLID Principles Applied:
    - S: Only responsible for loading model and running inference
    - O: Can extend with different model backends without modifying this class
    - D: Depends on abstraction (model path), not concrete implementation

    Why separate class?
    - Testable in isolation (without FastAPI)
    - Reusable in other modules (e.g., batch processing script)
    - Model loaded once at init, not per-request
    """

    def __init__(self, model_name: str = MODEL_NAME):
        """
        Load YOLO model.

        Args:
            model_name: Model filename (auto-downloads from ultralytics hub)
        """
        self.model_name = model_name
        logger.info(f"Loading YOLO model: {model_name}...")

        try:
            self.model = YOLO(model_name)
            logger.info(f"Model loaded successfully: {model_name}")
        except Exception as e:
            logger.critical(f"Failed to load YOLO model: {e}")
            raise

    def detect(self, image: np.ndarray, confidence_threshold: float = DEFAULT_CONFIDENCE_THRESHOLD) -> dict:
        """
        Run object detection on an image.

        Args:
            image: OpenCV image (BGR numpy array)
            confidence_threshold: Minimum confidence to include detection (0-1)

        Returns:
            Dict with:
            - detections: list of detected objects
            - processing_time_ms: inference time in milliseconds
            - total_objects: count of detected objects
        """
        start_time = time.time()

        # Run YOLO inference
        results = self.model(image, conf=confidence_threshold, verbose=False)

        processing_time_ms = int((time.time() - start_time) * 1000)

        # Parse results
        detections = []
        if results and len(results) > 0:
            result = results[0]
            img_height, img_width = image.shape[:2]

            for box in result.boxes:
                # Get bounding box (normalized 0-1 for DB storage)
                x1, y1, x2, y2 = box.xyxy[0].tolist()
                bbox_normalized = {
                    "x1": round(x1 / img_width, 4),
                    "y1": round(y1 / img_height, 4),
                    "x2": round(x2 / img_width, 4),
                    "y2": round(y2 / img_height, 4),
                }

                # Get pixel bounding box (for display/debugging)
                bbox_pixels = {
                    "x1": int(x1),
                    "y1": int(y1),
                    "x2": int(x2),
                    "y2": int(y2),
                }

                confidence = round(float(box.conf[0]), 4)
                class_id = int(box.cls[0])
                object_class = result.names[class_id]

                detections.append({
                    "object_class": object_class,
                    "confidence": confidence,
                    "bbox": bbox_normalized,
                    "bbox_pixels": bbox_pixels,
                    "distance_meters": None,  # Placeholder — needs depth estimation
                })

        return {
            "detections": detections,
            "processing_time_ms": processing_time_ms,
            "total_objects": len(detections),
        }


# ===========================================================================
# Lifespan — startup and shutdown logic
# ===========================================================================
# Load model at startup (not per-request)
detector: Optional[YoloDetector] = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Modern lifespan handler
    
    - Before yield: startup logic (load model, log startup)
    - After yield: shutdown logic (log shutdown)
    """
    global detector
    detector = YoloDetector(MODEL_NAME)

    if DB_AVAILABLE:
        try:
            system_logger.info(
                component="vision-ai",
                message="Vision AI server started",
                event_type="startup",
                details={"model": MODEL_NAME, "threshold": DEFAULT_CONFIDENCE_THRESHOLD}
            )
        except Exception as e:
            logger.warning(f"Failed to log startup to DB: {e}")

    yield  # --- Server is running ---

    if DB_AVAILABLE:
        try:
            system_logger.info(
                component="vision-ai",
                message="Vision AI server shutting down",
                event_type="shutdown",
            )
        except Exception:
            pass


# ===========================================================================
# FastAPI Application
# ===========================================================================
app = FastAPI(
    title="AGV Vision AI",
    description="YOLOv11s object detection API for AGV warehouse automation",
    version="1.0.0",
    lifespan=lifespan,
)


# ---------------------------------------------------------------------------
# Helper: Log detections to database
# ---------------------------------------------------------------------------
def _log_detections_to_db(detections: list, processing_time_ms: int,
                          image_path: Optional[str] = None) -> None:
    """
    Log detection results to PostgreSQL (fire-and-forget).

    Does NOT raise exceptions — DB failure should never break detection API.
    """
    if not DB_AVAILABLE:
        return

    try:
        for det in detections:
            detection_logger.log_detection(
                object_class=det["object_class"],
                confidence=det["confidence"],
                bbox=det["bbox"],
                distance_meters=det["distance_meters"],
                processing_time_ms=processing_time_ms,
                image_path=image_path,
                triggered_stop=False,
            )
    except Exception as e:
        logger.warning(f"Failed to log detections to DB: {e}")


# ---------------------------------------------------------------------------
# Helper: Read image from bytes
# ---------------------------------------------------------------------------
def _read_image_from_bytes(image_bytes: bytes) -> np.ndarray:
    """
    Convert raw bytes to OpenCV image.

    Raises:
        HTTPException: If image cannot be decoded
    """
    nparr = np.frombuffer(image_bytes, np.uint8)
    image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    if image is None:
        raise HTTPException(status_code=400, detail="Invalid image file — cannot decode")
    return image


# ===========================================================================
# API Endpoints
# ===========================================================================

@app.get("/health")
async def health_check():
    """
    Health check endpoint.

    Returns model status and database connectivity.
    Used by agv-control (C#) to verify Vision AI is running.
    """
    return {
        "status": "ok",
        "model": MODEL_NAME,
        "db_connected": DB_AVAILABLE,
    }


@app.post("/detect")
async def detect_objects(
    file: UploadFile = File(...),
    threshold: float = Query(
        default=DEFAULT_CONFIDENCE_THRESHOLD,
        ge=0.0,
        le=1.0,
        description="Minimum confidence threshold (0-1)"
    ),
):
    """
    Detect objects in uploaded image.

    This is the primary endpoint called by agv-control (C#):
    1. C# reads camera/images/latest.jpg
    2. C# sends POST /detect with the image
    3. This endpoint returns detected obstacles
    4. C# uses results for path planning (A*)

    Args:
        file: Image file (JPEG, PNG)
        threshold: Confidence threshold (default 0.5)

    Returns:
        JSON with detections, processing_time_ms, total_objects
    """
    # Read uploaded image
    image_bytes = await file.read()
    image = _read_image_from_bytes(image_bytes)

    # Run detection
    result = detector.detect(image, confidence_threshold=threshold)

    # Log to database (non-blocking, fire-and-forget)
    _log_detections_to_db(
        result["detections"],
        result["processing_time_ms"],
        image_path=file.filename,
    )

    logger.info(
        f"Detected {result['total_objects']} objects "
        f"in {result['processing_time_ms']}ms "
        f"(threshold={threshold})"
    )

    return result


@app.get("/detect/latest")
async def detect_latest(
    threshold: float = Query(
        default=DEFAULT_CONFIDENCE_THRESHOLD,
        ge=0.0,
        le=1.0,
        description="Minimum confidence threshold (0-1)"
    ),
):
    """
    Detect objects from camera's latest captured image.

    Reads from camera/images/latest.jpg directly.
    Useful for quick testing without uploading a file.

    Returns:
        JSON with detections, processing_time_ms, total_objects
    """
    if not CAMERA_IMAGE_PATH.exists():
        raise HTTPException(
            status_code=404,
            detail=f"No image found at {CAMERA_IMAGE_PATH}. Is camera module running?"
        )

    # Read image from file
    image = cv2.imread(str(CAMERA_IMAGE_PATH))
    if image is None:
        raise HTTPException(
            status_code=500,
            detail=f"Failed to read image at {CAMERA_IMAGE_PATH}"
        )

    # Run detection
    result = detector.detect(image, confidence_threshold=threshold)

    # Log to database
    _log_detections_to_db(
        result["detections"],
        result["processing_time_ms"],
        image_path=str(CAMERA_IMAGE_PATH),
    )

    logger.info(
        f"[latest] Detected {result['total_objects']} objects "
        f"in {result['processing_time_ms']}ms"
    )

    return result


# ===========================================================================
# Entry Point
# ===========================================================================
if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
