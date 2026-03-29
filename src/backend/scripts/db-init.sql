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






-- add super ADMIN

-- Step 1: get the user ID (copy the result for steps below)
SELECT "id" FROM identity."users"
WHERE "email" = 'admin@acme.test';

-- Step 2: insert a platform-level role (TenantId = Guid.Empty = system sentinel)
INSERT INTO rbac."Roles" (
    "Id", "TenantId", "Name", "Description",
    "IsSystemRole", "IsDeleted", "CreatedAt", "CreatedBy"
)
VALUES (
    '00000000-0000-0000-0000-000000000001',  -- fixed ID for platform:super-admin role
    '00000000-0000-0000-0000-000000000000',  -- Guid.Empty = platform-level, not tenant-scoped
    'platform:super-admin',
    'Platform-level super admin — bypasses all tenant isolation.',
    TRUE, FALSE, NOW(), '00000000-0000-0000-0000-000000000000'
)
ON CONFLICT DO NOTHING;

-- Step 3: assign the role to admin@acme.test
-- Replace <user-id> with the ID from Step 1
INSERT INTO rbac."UserRoleAssignments" (
    "Id",
    "TenantId",
    "UserId",
    "RoleId",
    "ScopeId",
    "IsActive",
    "ExpiresAt",
    "DeactivatedReason",
    "DeactivatedAt",
    "IsDeleted",
    "DeletedAt",
    "DeletedBy",
    "CreatedAt",
    "CreatedBy",
    "UpdatedAt",
    "UpdatedBy"
)
VALUES (
    gen_random_uuid(),
    '00000000-0000-0000-0000-000000000000',
    '<user-id>',                              -- paste the ID from Step 1
    '00000000-0000-0000-0000-000000000001',
    NULL,
    TRUE,
    NULL,
    NULL,
    NULL,
    FALSE,
    NULL,
    NULL,
    NOW(),
    '00000000-0000-0000-0000-000000000000',
    NULL,
    NULL
);
