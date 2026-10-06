-- Runs once on first postgres container init.
-- Database-per-service: each SCS service owns exactly one database.
CREATE DATABASE scs_todos;
CREATE DATABASE scs_notifications;
