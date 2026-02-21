"""
Camera Capture Module for AGV Vision System
============================================
Captures frames from USB webcam and saves to shared directory.

Clean Architecture:
- Single Responsibility: Only handles camera I/O
- Dependency Injection: Camera source configurable
- KISS: Simple capture loop, no complex logic
"""

import cv2
import time
import logging
from pathlib import Path
from datetime import datetime
from typing import Optional

# Import database logger
try:
    from common.db_logger import system_logger
    DB_ENABLED = True
except ImportError:
    logging.warning("Database logger not found. Running without DB integration.")
    logging.warning("Install: pip install psycopg2-binary")
    DB_ENABLED = False

# Configuration
CAMERA_ID = 0  # USB webcam (0 = default, 1 = external)
BASE_DIR = Path(__file__).resolve().parent
OUTPUT_DIR = BASE_DIR / "images" 
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
OUTPUT_FILE = OUTPUT_DIR / "latest.jpg"
CAPTURE_INTERVAL = 1.0  # seconds
IMAGE_WIDTH = 640
IMAGE_HEIGHT = 480

# Logging setup
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger("camera")


class CameraCapture:
    """
    Handles USB webcam capture following SRP (Single Responsibility Principle).
    
    SOLID Principles Applied:
    - S: Only responsible for camera I/O
    - O: Can extend with different camera sources
    - L: Substitutable with IP camera implementation
    - I: Interface segregation (only capture method exposed)
    - D: Depends on cv2 abstraction, not concrete hardware
    """
    
    def __init__(self, camera_id: int = CAMERA_ID, 
                 width: int = IMAGE_WIDTH, 
                 height: int = IMAGE_HEIGHT):
        """
        Initialize camera with specified parameters.
        
        Args:
            camera_id: OpenCV camera index (0 for default webcam)
            width: Frame width in pixels
            height: Frame height in pixels
        """
        self.camera_id = camera_id
        self.width = width
        self.height = height
        self.cap: Optional[cv2.VideoCapture] = None
        
    def open(self) -> bool:
        """
        Open camera connection.
        
        Returns:
            True if camera opened successfully, False otherwise
            
        Design Pattern: Fail-fast validation
        """
        logger.info(f"Opening camera {self.camera_id}...")
        self.cap = cv2.VideoCapture(self.camera_id)
        
        if not self.cap.isOpened():
            logger.error(f"Failed to open camera {self.camera_id}")
            return False
        
        # Set camera resolution
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, self.width)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, self.height)
        
        # Verify settings
        actual_width = int(self.cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        actual_height = int(self.cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        logger.info(f"Camera opened: {actual_width}x{actual_height}")
        
        return True
    
    def capture_frame(self) -> Optional[cv2.Mat]:
        """
        Capture a single frame from camera.
        
        Returns:
            Frame as numpy array, or None if capture failed
        """
        if self.cap is None or not self.cap.isOpened():
            logger.error("Camera not opened")
            return None
        
        ret, frame = self.cap.read()
        
        if not ret:
            logger.error("Failed to read frame")
            return None
        
        return frame
    
    def close(self) -> None:
        """Release camera resources."""
        if self.cap is not None:
            self.cap.release()
            logger.info("Camera closed")


class ImageSaver:
    """
    Handles image saving operations (SRP: Separate concern from capture).
    
    Why separate class?
    - Capture and Save are different responsibilities
    - Easy to swap storage backend (local, S3, etc.)
    - Testable independently
    """
    
    def __init__(self, output_dir: Path):
        """
        Initialize saver with output directory.
        
        Args:
            output_dir: Directory to save images
        """
        self.output_dir = output_dir
        self._ensure_directory()
    
    def _ensure_directory(self) -> None:
        """Create output directory if it doesn't exist."""
        self.output_dir.mkdir(parents=True, exist_ok=True)
        logger.info(f"Output directory: {self.output_dir.absolute()}")
    
    def save(self, frame: cv2.Mat, filename: str = "latest.jpg") -> bool:
        """
        Save frame to file.
        
        Args:
            frame: Image frame to save
            filename: Output filename
            
        Returns:
            True if save successful, False otherwise
        """
        filepath = self.output_dir / filename
        
        try:
            cv2.imwrite(str(filepath), frame)
            logger.info(f"Saved: {filepath}")
            return True
        except Exception as e:
            logger.error(f"Failed to save {filepath}: {e}")
            return False
    
    def save_timestamped(self, frame: cv2.Mat) -> bool:
        """
        Save frame with timestamp in filename.
        
        Useful for debugging/logging, not used in production loop.
        """
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        filename = f"capture_{timestamp}.jpg"
        return self.save(frame, filename)


def main():
    """
    Main capture loop.
    
    DRY Principle: Reusable components (Camera, Saver)
    KISS Principle: Simple infinite loop, no overengineering
    """
    logger.info("=" * 60)
    logger.info("AGV Camera Capture Server")
    logger.info("=" * 60)
    
    # Initialize components (Dependency Injection pattern)
    camera = CameraCapture(CAMERA_ID, IMAGE_WIDTH, IMAGE_HEIGHT)
    saver = ImageSaver(OUTPUT_DIR)
    
    # Log startup to database
    if DB_ENABLED:
        system_logger.info(
            component='camera',
            message='Camera server started',
            event_type='startup',
            details={
                'camera_id': CAMERA_ID,
                'resolution': f'{IMAGE_WIDTH}x{IMAGE_HEIGHT}',
                'capture_interval': CAPTURE_INTERVAL
            }
        )
    
    # Open camera
    if not camera.open():
        logger.critical("Cannot start without camera")
        
        if DB_ENABLED:
            system_logger.critical(
                component='camera',
                message='Failed to open camera - server cannot start',
                event_type='camera_open_failed',
                details={'camera_id': CAMERA_ID}
            )
        return
    
    logger.info(f"Capture interval: {CAPTURE_INTERVAL}s")
    logger.info("Press Ctrl+C to stop")
    logger.info("-" * 60)
    
    try:
        frame_count = 0
        error_count = 0
        
        while True:
            # Capture frame
            frame = camera.capture_frame()
            
            if frame is None:
                logger.warning("Skipping frame due to capture failure")
                error_count += 1
                
                # Log repeated errors to database
                if DB_ENABLED and error_count % 10 == 0:
                    system_logger.warning(
                        component='camera',
                        message=f'Camera capture failed {error_count} times',
                        event_type='capture_failure',
                        details={'total_errors': error_count, 'frames_captured': frame_count}
                    )
                
                time.sleep(CAPTURE_INTERVAL)
                continue
            
            # Save frame (overwrite latest.jpg for Vision AI to read)
            if saver.save(frame):
                frame_count += 1
                logger.info(f"Frame #{frame_count} captured successfully")
                
                # Log milestone to database (every 100 frames)
                if DB_ENABLED and frame_count % 100 == 0:
                    system_logger.info(
                        component='camera',
                        message=f'Camera milestone: {frame_count} frames captured',
                        event_type='capture_milestone',
                        details={'frames_captured': frame_count, 'errors': error_count}
                    )
            
            # Wait for next capture
            time.sleep(CAPTURE_INTERVAL)
            
    except KeyboardInterrupt:
        logger.info("\nShutdown requested by user")
        
        if DB_ENABLED:
            system_logger.info(
                component='camera',
                message='Camera server stopped by user',
                event_type='shutdown',
                details={'frames_captured': frame_count, 'errors': error_count}
            )
            
    except Exception as e:
        logger.exception(f"Unexpected error: {e}")
        
        if DB_ENABLED:
            system_logger.error(
                component='camera',
                message=f'Unexpected error: {str(e)}',
                event_type='unexpected_error',
                details={'error': str(e), 'frames_captured': frame_count}
            )
    finally:
        camera.close()
        logger.info("Camera server stopped")


if __name__ == "__main__":
    main()