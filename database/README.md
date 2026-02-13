# Database Schema

PostgreSQL database for AGV system logging.

## Setup

```bash
psql -U postgres
CREATE DATABASE agv_vision_db;
\c agv_vision_db
\i init.sql
```

## Tables

- `detections` - Vision AI detection results
- `paths` - AGV path planning history
- `system_logs` - Centralized system logging
