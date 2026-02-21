"""
Database Connection Module
===========================
Centralized PostgreSQL connection and logging for AGV system.

Design Principles:
- Single Responsibility: Only handles DB operations
- Dependency Injection: Connection string configurable
- Error Handling: Graceful failure with logging
"""

import psycopg2
from psycopg2.extras import Json
from typing import Optional, List, Dict, Any
import logging
from datetime import datetime
from contextlib import contextmanager

logger = logging.getLogger(__name__)


# Database Configuration
DB_CONFIG = {
    'host': 'localhost',
    'port': 5432,
    'database': 'agv_control_db',
    'user': 'postgres',
    'password': '1111'  # CHANGE IN PRODUCTION!
}


class DatabaseConnection:
    """
    PostgreSQL database connection manager (Singleton pattern).
    
    Why Singleton?
    - Only one connection pool per application
    - Avoid multiple connection overhead
    - Thread-safe connection reuse
    """
    
    _instance: Optional['DatabaseConnection'] = None
    _connection: Optional[psycopg2.extensions.connection] = None
    
    def __new__(cls):
        """Singleton pattern implementation."""
        if cls._instance is None:
            cls._instance = super().__new__(cls)
        return cls._instance
    
    def __init__(self):
        """Initialize singleton state without forcing DB connection on import."""
        # Keep init lightweight so modules can import even when DB is offline.
        # The actual connection is created lazily in get_cursor().
        pass

    def _is_connection_open(self) -> bool:
        """Return True when psycopg2 connection exists and is open."""
        return self._connection is not None and self._connection.closed == 0
         
    def connect(self) -> None:
        """
        Establish database connection.
        
        Raises:
            psycopg2.Error: If connection fails
        """
        try:
            self._connection = psycopg2.connect(**DB_CONFIG)
            logger.info(f"✓ Connected to database: {DB_CONFIG['database']}")
        except psycopg2.Error as e:
            logger.error(f"✗ Database connection failed: {e}")
            raise
    
    @contextmanager
    def get_cursor(self):
        """
        Context manager for database cursor.
        
        Usage:
            with db.get_cursor() as cur:
                cur.execute("SELECT * FROM detections")
        
        Benefits:
        - Automatic commit on success
        - Automatic rollback on error
        - Automatic cursor cleanup
        """
        if not self._is_connection_open():
            self.connect()
        
        cursor = self._connection.cursor()
        try:
            yield cursor
            self._connection.commit()
        except Exception as e:
            self._connection.rollback()
            logger.error(f"Database error: {e}")
            raise
        finally:
            cursor.close()
    
    def close(self) -> None:
        """Close database connection."""
        if self._is_connection_open():
            self._connection.close()
            logger.info("Database connection closed")


class DetectionLogger:
    """
    Handles logging to 'detections' table.
    
    Business Case: "AGV collided. Did AI detect the obstacle?"
    """
    
    def __init__(self):
        self.db = DatabaseConnection()
    
    def log_detection(self,
                     object_class: str,
                     confidence: float,
                     bbox: Dict[str, float],
                     distance_meters: Optional[float] = None,
                     processing_time_ms: Optional[int] = None,
                     image_path: Optional[str] = None,
                     triggered_stop: bool = False) -> Optional[int]:
        """
        Insert detection result into database.
        
        Args:
            object_class: Detected object type (e.g., 'person', 'box')
            confidence: Detection confidence (0-1)
            bbox: Bounding box {x1, y1, x2, y2} (normalized 0-1)
            distance_meters: Estimated distance
            processing_time_ms: YOLO inference time
            image_path: Path to source image
            triggered_stop: Did this detection trigger emergency stop?
            
        Returns:
            Detection ID if successful, None if failed
            
        Example:
            logger.log_detection(
                object_class='person',
                confidence=0.89,
                bbox={'x1': 0.12, 'y1': 0.34, 'x2': 0.45, 'y2': 0.89},
                distance_meters=3.2,
                processing_time_ms=45,
                triggered_stop=True
            )
        """
        query = """
        INSERT INTO detections (
            timestamp, image_path, processing_time_ms,
            object_class, confidence,
            bbox_x1, bbox_y1, bbox_x2, bbox_y2,
            distance_meters, triggered_stop
        )
        VALUES (
            NOW(), %s, %s,
            %s, %s,
            %s, %s, %s, %s,
            %s, %s
        )
        RETURNING id;
        """
        
        try:
            with self.db.get_cursor() as cur:
                cur.execute(query, (
                    image_path,
                    processing_time_ms,
                    object_class,
                    confidence,
                    bbox.get('x1'),
                    bbox.get('y1'),
                    bbox.get('x2'),
                    bbox.get('y2'),
                    distance_meters,
                    triggered_stop
                ))
                
                detection_id = cur.fetchone()[0]
                logger.debug(f"Logged detection #{detection_id}: {object_class} (conf={confidence:.2f})")
                return detection_id
                
        except Exception as e:
            logger.error(f"Failed to log detection: {e}")
            return None
    
    def log_batch_detections(self, detections: List[Dict[str, Any]]) -> int:
        """
        Log multiple detections in a single transaction.
        
        Args:
            detections: List of detection dicts
            
        Returns:
            Number of detections logged
            
        More efficient than calling log_detection() multiple times.
        """
        query = """
        INSERT INTO detections (
            timestamp, object_class, confidence,
            bbox_x1, bbox_y1, bbox_x2, bbox_y2,
            distance_meters, processing_time_ms, triggered_stop
        )
        VALUES (NOW(), %s, %s, %s, %s, %s, %s, %s, %s, %s);
        """
        
        count = 0
        try:
            with self.db.get_cursor() as cur:
                for det in detections:
                    bbox = det.get('bbox', {})
                    cur.execute(query, (
                        det.get('object_class'),
                        det.get('confidence'),
                        bbox.get('x1'),
                        bbox.get('y1'),
                        bbox.get('x2'),
                        bbox.get('y2'),
                        det.get('distance_meters'),
                        det.get('processing_time_ms'),
                        det.get('triggered_stop', False)
                    ))
                    count += 1
                
                logger.info(f"Batch logged {count} detections")
                return count
                
        except Exception as e:
            logger.error(f"Batch logging failed: {e}")
            return count


class SystemLogger:
    """
    Handles logging to 'system_logs' table.
    
    Business Case: "Daily report: trips, errors, battery warnings?"
    """
    
    def __init__(self):
        self.db = DatabaseConnection()
    
    def log_event(self,
                  level: str,
                  component: str,
                  event_type: str,
                  message: str,
                  details: Optional[Dict[str, Any]] = None,
                  agv_speed_mms: Optional[int] = None,
                  battery_percentage: Optional[int] = None,
                  position_x: Optional[float] = None,
                  position_y: Optional[float] = None,
                  path_id: Optional[int] = None,
                  detection_id: Optional[int] = None) -> Optional[int]:
        """
        Insert system event into logs.
        
        Args:
            level: Log level (DEBUG, INFO, WARNING, ERROR, CRITICAL)
            component: Module name (camera, vision-ai, agv-control, hardware-sim)
            event_type: Event category (battery_low, collision, api_timeout, etc.)
            message: Human-readable message
            details: Additional JSON metadata
            agv_speed_mms: AGV speed in mm/s
            battery_percentage: Battery level 0-100
            position_x, position_y: AGV coordinates
            path_id: Related path ID (FK)
            detection_id: Related detection ID (FK)
            
        Returns:
            Log ID if successful, None if failed
            
        Example:
            logger.log_event(
                level='WARNING',
                component='vision-ai',
                event_type='api_timeout',
                message='YOLO inference timeout after 5s',
                battery_percentage=18,
                agv_speed_mms=0
            )
        """
        query = """
        INSERT INTO system_logs (
            timestamp, level, component, event_type, message, details,
            agv_speed_mms, battery_percentage, position_x, position_y,
            path_id, detection_id
        )
        VALUES (
            NOW(), %s, %s, %s, %s, %s,
            %s, %s, %s, %s,
            %s, %s
        )
        RETURNING id;
        """
        
        try:
            with self.db.get_cursor() as cur:
                cur.execute(query, (
                    level,
                    component,
                    event_type,
                    message,
                    Json(details) if details else None,
                    agv_speed_mms,
                    battery_percentage,
                    position_x,
                    position_y,
                    path_id,
                    detection_id
                ))
                
                log_id = cur.fetchone()[0]
                logger.debug(f"Logged system event #{log_id}: [{level}] {message}")
                return log_id
                
        except Exception as e:
            logger.error(f"Failed to log system event: {e}")
            return None
 
    # Convenience methods for different log levels

    def debug(self, component: str, message: str, **kwargs):
        """Log DEBUG level event."""
        return self.log_event('DEBUG', component, kwargs.pop('event_type', 'debug'), message, **kwargs)
    
    def info(self, component: str, message: str, **kwargs):
        """Log INFO level event."""
        return self.log_event('INFO', component, kwargs.pop('event_type', 'info'), message, **kwargs)
    
    def warning(self, component: str, message: str, **kwargs):
        """Log WARNING level event."""
        return self.log_event('WARNING', component, kwargs.pop('event_type', 'warning'), message, **kwargs)
    
    def error(self, component: str, message: str, **kwargs):
        """Log ERROR level event."""
        return self.log_event('ERROR', component, kwargs.pop('event_type', 'error'), message, **kwargs)
    
    def critical(self, component: str, message: str, **kwargs):
        """Log CRITICAL level event."""
        return self.log_event('CRITICAL', component, kwargs.pop('event_type', 'critical'), message, **kwargs)


# Singleton instances for easy import
detection_logger = DetectionLogger()
system_logger = SystemLogger()


# Example usage
if __name__ == "__main__":
    import sys

    # Test database connection
    logging.basicConfig(level=logging.INFO)

    detection_id = detection_logger.log_detection(
        object_class='person',
        confidence=0.89,
        bbox={'x1': 0.12, 'y1': 0.34, 'x2': 0.45, 'y2': 0.89},
        distance_meters=3.2,
        processing_time_ms=45,
        triggered_stop=True
    )

    system_log_id = system_logger.info(
        component='vision-ai',
        message='Vision AI module started successfully',
        event_type='startup',
        details={'model': 'yolo11n.pt', 'confidence_threshold': 0.5}
    )

    if detection_id is not None and system_log_id is not None:
        print("✓ Database logging test successful")
        sys.exit(0)

    print("✗ Database logging test failed")
    print(f"  detection_log_id={detection_id}, system_log_id={system_log_id}")
    sys.exit(1)
