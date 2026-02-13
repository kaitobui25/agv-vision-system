# Workflow: Database Setup

## Goal
Create PostgreSQL schema with business-driven tables

## Steps
1. Read `docs/PROJECT_NOTES.md` → understand business cases
2. Write `database/init.sql`:
   - detections (vision AI logs)
   - paths (route history)
   - system_logs (errors, battery, events)
3. Test:
```bash
   psql -U postgres
   CREATE DATABASE agv_vision_db;
   \c agv_vision_db
   \i database/init.sql
```
4. Verify: Run sample queries for 3 business cases

## Success Criteria
- [ ] All tables created
- [ ] Foreign keys work
- [ ] Can answer: "Did AI detect before collision?"