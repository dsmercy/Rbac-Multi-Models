-- ============================================================
-- RBAC System — PostgreSQL schema initialization
-- Creates all schemas and grants. EF Core migrations handle
-- table creation — this script only sets up schemas + roles.
-- ============================================================

-- Create module schemas
CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS tenant;
CREATE SCHEMA IF NOT EXISTS rbac;
CREATE SCHEMA IF NOT EXISTS policy;
CREATE SCHEMA IF NOT EXISTS delegation;
CREATE SCHEMA IF NOT EXISTS audit;

-- Grant schema access to application user
GRANT USAGE, CREATE ON SCHEMA identity    TO rbac_user;
GRANT USAGE, CREATE ON SCHEMA tenant      TO rbac_user;
GRANT USAGE, CREATE ON SCHEMA rbac        TO rbac_user;
GRANT USAGE, CREATE ON SCHEMA policy      TO rbac_user;
GRANT USAGE, CREATE ON SCHEMA delegation  TO rbac_user;
GRANT USAGE, CREATE ON SCHEMA audit       TO rbac_user;

-- Ensure rbac_user owns its schemas for migrations
ALTER SCHEMA identity    OWNER TO rbac_user;
ALTER SCHEMA tenant      OWNER TO rbac_user;
ALTER SCHEMA rbac        OWNER TO rbac_user;
ALTER SCHEMA policy      OWNER TO rbac_user;
ALTER SCHEMA delegation  OWNER TO rbac_user;
ALTER SCHEMA audit       OWNER TO rbac_user;

-- Audit schema: restrict DELETE/UPDATE at database level
-- Application enforces this, but belt-and-suspenders via a trigger
CREATE OR REPLACE FUNCTION audit.prevent_audit_modification()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'AuditLogs are immutable. UPDATE and DELETE operations are forbidden.';
END;
$$ LANGUAGE plpgsql;
