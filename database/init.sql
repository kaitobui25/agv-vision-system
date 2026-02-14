-- ============================================================================
-- AGV Vision Control System - Database Schema
-- ============================================================================
-- Purpose: Business-driven schema to answer 3 key questions:
--   1. Collision Investigation: Did AI detect the obstacle?
--   2. Route Optimization: What path did AGV take? How long?
--   3. Daily Operations: How many trips? Any errors? Battery status?
-- ============================================================================

-- Clean slate (be careful in production!)
DROP TABLE IF EXISTS detections CASCADE;
DROP TABLE IF EXISTS paths CASCADE;
DROP TABLE IF EXISTS system_logs CASCADE;

-- ============================================================================
-- TABLE: detections
-- ============================================================================
-- Purpose: Log all Vision AI detection results
-- Business Case: "AGV collided. Did YOLO detect the box before crash?"
-- ============================================================================
CREATE TABLE detections (
    id BIGSERIAL PRIMARY KEY,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Detection metadata
    image_path VARCHAR(255),
    processing_time_ms INTEGER,  -- YOLO inference latency
    
    -- Object detection results
    object_class VARCHAR(50) NOT NULL,  -- 'box', 'person', 'forklift', 'pallet'
    confidence DECIMAL(5,4) NOT NULL CHECK (confidence >= 0 AND confidence <= 1),
    
    -- Bounding box (normalized 0-1)
    bbox_x1 DECIMAL(6,5) CHECK (bbox_x1 >= 0 AND bbox_x1 <= 1),
    bbox_y1 DECIMAL(6,5) CHECK (bbox_y1 >= 0 AND bbox_y1 <= 1),
    bbox_x2 DECIMAL(6,5) CHECK (bbox_x2 >= 0 AND bbox_x2 <= 1),
    bbox_y2 DECIMAL(6,5) CHECK (bbox_y2 >= 0 AND bbox_y2 <= 1),
    
    -- Estimated distance (from camera/LiDAR fusion)
    distance_meters DECIMAL(5,2) CHECK (distance_meters >= 0),
    
    -- Action taken
    triggered_stop BOOLEAN DEFAULT FALSE,
    
    CONSTRAINT valid_bbox CHECK (bbox_x2 >= bbox_x1 AND bbox_y2 >= bbox_y1)
);

-- Index for collision investigation queries
CREATE INDEX idx_detections_timestamp ON detections(timestamp DESC);
CREATE INDEX idx_detections_object_class ON detections(object_class);
CREATE INDEX idx_detections_high_confidence ON detections(confidence DESC) WHERE confidence >= 0.7;

COMMENT ON TABLE detections IS 'Vision AI detection logs for obstacle analysis';
COMMENT ON COLUMN detections.confidence IS 'YOLO confidence score (0-1). Threshold typically 0.5-0.7';
COMMENT ON COLUMN detections.triggered_stop IS 'Did this detection trigger emergency stop?';

-- ============================================================================
-- TABLE: paths
-- ============================================================================
-- Purpose: AGV route history and planning results
-- Business Case: "AGV took 5min for 20m. What route? Any replanning?"
-- ============================================================================
CREATE TABLE paths (
    id BIGSERIAL PRIMARY KEY,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    
    -- Route definition
    start_x DECIMAL(8,2) NOT NULL,  -- mm precision
    start_y DECIMAL(8,2) NOT NULL,
    end_x DECIMAL(8,2) NOT NULL,
    end_y DECIMAL(8,2) NOT NULL,
    
    -- A* algorithm results
    waypoints JSONB,  -- [{x: 100, y: 200, timestamp: "..."}, ...]
    total_distance_mm DECIMAL(10,2),
    
    -- Performance metrics
    planned_duration_sec INTEGER,
    actual_duration_sec INTEGER,
    replanning_count INTEGER DEFAULT 0,  -- How many times route was recalculated
    
    -- Status
    status VARCHAR(20) DEFAULT 'planned',  -- planned, executing, completed, aborted
    abort_reason VARCHAR(255),
    
    -- Link to detections that caused replanning
    related_detection_ids BIGINT[],
    
    CONSTRAINT valid_duration CHECK (actual_duration_sec IS NULL OR actual_duration_sec >= 0)
);

CREATE INDEX idx_paths_created_at ON paths(created_at DESC);
CREATE INDEX idx_paths_status ON paths(status);
CREATE INDEX idx_paths_completed ON paths(completed_at DESC) WHERE completed_at IS NOT NULL;

COMMENT ON TABLE paths IS 'AGV route history for optimization analysis';
COMMENT ON COLUMN paths.waypoints IS 'JSON array of {x, y, timestamp} points from A* algorithm';
COMMENT ON COLUMN paths.replanning_count IS 'Number of times route was recalculated due to obstacles';

-- ============================================================================
-- TABLE: system_logs
-- ============================================================================
-- Purpose: Centralized event logging (errors, battery, system events)
-- Business Case: "Daily report: trips count, errors, battery warnings?"
-- ============================================================================
CREATE TABLE system_logs (
    id BIGSERIAL PRIMARY KEY,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Event classification
    level VARCHAR(10) NOT NULL,  -- DEBUG, INFO, WARNING, ERROR, CRITICAL
    component VARCHAR(50) NOT NULL,  -- camera, vision-ai, agv-control, hardware-sim
    event_type VARCHAR(50) NOT NULL,  -- battery_low, collision, modbus_error, api_timeout
    
    -- Event details
    message TEXT NOT NULL,
    details JSONB,  -- Flexible JSON for component-specific data
    
    -- System state snapshot
    agv_speed_mms INTEGER,  -- mm/s
    battery_percentage INTEGER CHECK (battery_percentage >= 0 AND battery_percentage <= 100),
    position_x DECIMAL(8,2),
    position_y DECIMAL(8,2),
    
    -- Traceability
    path_id BIGINT REFERENCES paths(id) ON DELETE SET NULL,
    detection_id BIGINT REFERENCES detections(id) ON DELETE SET NULL,
    
    -- Error handling
    exception_type VARCHAR(100),
    stack_trace TEXT
);

CREATE INDEX idx_system_logs_timestamp ON system_logs(timestamp DESC);
CREATE INDEX idx_system_logs_level ON system_logs(level);
CREATE INDEX idx_system_logs_component ON system_logs(component);
CREATE INDEX idx_system_logs_event_type ON system_logs(event_type);
CREATE INDEX idx_system_logs_battery ON system_logs(battery_percentage) WHERE battery_percentage IS NOT NULL;

COMMENT ON TABLE system_logs IS 'Centralized logging for daily operations and troubleshooting';
COMMENT ON COLUMN system_logs.level IS 'Log severity: DEBUG, INFO, WARNING, ERROR, CRITICAL';
COMMENT ON COLUMN system_logs.details IS 'Flexible JSON for component-specific metadata';

-- ============================================================================
-- SAMPLE DATA (for testing business cases)
-- ============================================================================

-- Case 1: Collision investigation - AI detected box but confidence was low
INSERT INTO detections (timestamp, object_class, confidence, distance_meters, triggered_stop, processing_time_ms)
VALUES 
    (NOW() - INTERVAL '2 hours', 'box', 0.45, 2.5, FALSE, 120),  -- Low confidence, no stop
    (NOW() - INTERVAL '2 hours' + INTERVAL '500 ms', 'box', 0.52, 2.3, FALSE, 115),
    (NOW() - INTERVAL '2 hours' + INTERVAL '1 second', 'box', 0.78, 1.8, TRUE, 118);  -- Finally triggered

-- Case 2: Route optimization - AGV replanned 3 times
INSERT INTO paths (start_x, start_y, end_x, end_y, waypoints, total_distance_mm, replanning_count, status, created_at, completed_at, actual_duration_sec)
VALUES (
    0, 0, 20000, 10000,
    '[{"x": 0, "y": 0}, {"x": 5000, "y": 2000}, {"x": 12000, "y": 8000}, {"x": 20000, "y": 10000}]'::jsonb,
    25430,
    3,
    'completed',
    NOW() - INTERVAL '1 hour',
    NOW() - INTERVAL '55 minutes',
    300  -- 5 minutes = 300 seconds
);

-- Case 3: Daily operations - various events
INSERT INTO system_logs (level, component, event_type, message, battery_percentage, agv_speed_mms)
VALUES 
    ('INFO', 'agv-control', 'trip_started', 'Starting route to warehouse B', 95, 0),
    ('WARNING', 'hardware-sim', 'battery_low', 'Battery level below 20%', 18, 800),
    ('ERROR', 'vision-ai', 'api_timeout', 'YOLO inference timeout after 5s', 18, 0),
    ('INFO', 'agv-control', 'trip_completed', 'Arrived at destination', 15, 0);

-- ============================================================================
-- BUSINESS QUERY EXAMPLES
-- ============================================================================

-- Query 1: Collision Investigation
-- "Did AI detect the obstacle before the collision at 14:30?"
-- 
-- SELECT 
--     timestamp,
--     object_class,
--     confidence,
--     distance_meters,
--     triggered_stop
-- FROM detections
-- WHERE timestamp BETWEEN '2024-01-15 14:28:00' AND '2024-01-15 14:31:00'
-- ORDER BY timestamp;

-- Query 2: Route Optimization
-- "Show routes that took longer than planned or had multiple replannings"
--
-- SELECT 
--     id,
--     start_x, start_y, end_x, end_y,
--     total_distance_mm / 1000.0 AS distance_meters,
--     actual_duration_sec / 60.0 AS duration_minutes,
--     replanning_count,
--     status
-- FROM paths
-- WHERE replanning_count > 2 OR actual_duration_sec > planned_duration_sec * 1.5
-- ORDER BY created_at DESC;

-- Query 3: Daily Operations Report
-- "How many trips yesterday? Any critical errors?"
--
-- SELECT 
--     DATE(created_at) AS date,
--     COUNT(*) AS total_trips,
--     COUNT(*) FILTER (WHERE status = 'completed') AS completed_trips,
--     COUNT(*) FILTER (WHERE status = 'aborted') AS aborted_trips,
--     AVG(actual_duration_sec / 60.0) AS avg_duration_minutes
-- FROM paths
-- WHERE created_at >= CURRENT_DATE - INTERVAL '1 day'
-- GROUP BY DATE(created_at);
--
-- SELECT level, component, event_type, COUNT(*)
-- FROM system_logs
-- WHERE timestamp >= CURRENT_DATE - INTERVAL '1 day'
--   AND level IN ('ERROR', 'CRITICAL')
-- GROUP BY level, component, event_type
-- ORDER BY COUNT(*) DESC;

-- ============================================================================
-- GRANTS (adjust for your user)
-- ============================================================================
-- GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO agv_user;
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO agv_user;

-- ============================================================================
-- END OF SCHEMA
-- ============================================================================
