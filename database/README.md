# Database Schema

PostgreSQL database for AGV system logging.

## Setup

```bash
psql -U postgres
CREATE DATABASE agv_control_db;
\c agv_control_db
\i init.sql
```

## Tables

- `detections` - Vision AI detection results
- `paths` - AGV path planning history
- `system_logs` - Centralized system logging
