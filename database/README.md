# Database Schema

PostgreSQL database for AGV system logging.

## Database Architecture

![ERD](/docs/images/agv_control_db.png)

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

## Test who is connected to the database

```bash
SELECT pid, usename, client_addr, state
FROM pg_stat_activity
WHERE datname = 'agv_control_db';
```

## Disconnect all

```bash
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = 'agv_control_db'
  AND pid <> pg_backend_pid();
```

## Check current database

```bash
SELECT current_database();
```

## Change database

```bash
\c agv_control_db
```

## Disconnect psql server

CMD as Administrator

```bash
net stop postgresql-x64-18
```

## Start psql server

CMD as Administrator

```bash
net start postgresql-x64-18
```

## Display information about current connection

\conninfo
