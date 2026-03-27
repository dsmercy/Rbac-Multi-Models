-- =============================================================================
-- RBAC System — Phase 2: Complete Database Schema
-- PostgreSQL 16
-- Generated to match EF Core entity configurations from Phase 1
--
-- Schema layout (one PostgreSQL schema per bounded module):
--   tenant    → TenantManagement module
--   identity  → Identity module
--   rbac      → RbacCore module
--   policy    → PolicyEngine module
--   delegation→ Delegation module
--   audit     → AuditLogging module
--
-- Multi-tenancy model: row-level isolation — TenantId on every
-- tenant-scoped table, enforced by EF Core global query filters.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- Extensions
-- ---------------------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS "pgcrypto";   -- gen_random_uuid() fallback
CREATE EXTENSION IF NOT EXISTS "pg_trgm";    -- future: fuzzy search on names

-- ---------------------------------------------------------------------------
-- Schemas
-- ---------------------------------------------------------------------------
CREATE SCHEMA IF NOT EXISTS tenant;
CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS rbac;
CREATE SCHEMA IF NOT EXISTS policy;
CREATE SCHEMA IF NOT EXISTS delegation;
CREATE SCHEMA IF NOT EXISTS audit;


-- =============================================================================
-- SCHEMA: tenant
-- Tables: Tenants
-- No TenantId FK (this IS the tenant registry).
-- Soft-delete via IsDeleted / DeletedAt.
-- =============================================================================

CREATE TABLE tenant."Tenants" (
    -- Identity
    "Id"                            UUID            NOT NULL    DEFAULT gen_random_uuid(),

    -- Core fields
    "Name"                          VARCHAR(200)    NOT NULL,

    -- Owned value-object: TenantSlug
    "Slug"                          VARCHAR(63)     NOT NULL,

    -- Owned value-object: TenantConfiguration (flattened columns)
    "MaxDelegationChainDepth"       INT             NOT NULL    DEFAULT 1,
    "PermissionCacheTtlSeconds"     INT             NOT NULL    DEFAULT 300,
    "TokenVersionCacheTtlSeconds"   INT             NOT NULL    DEFAULT 3600,
    "MaxUsersAllowed"               INT             NOT NULL    DEFAULT 500,
    "MaxRolesAllowed"               INT             NOT NULL    DEFAULT 100,

    -- State
    "IsActive"                      BOOLEAN         NOT NULL    DEFAULT TRUE,
    "IsBootstrapped"                BOOLEAN         NOT NULL    DEFAULT FALSE,
    "SuspendedAt"                   TIMESTAMPTZ,
    "SuspensionReason"              VARCHAR(500),

    -- Soft-delete (SoftDeletableEntity)
    "IsDeleted"                     BOOLEAN         NOT NULL    DEFAULT FALSE,
    "DeletedAt"                     TIMESTAMPTZ,
    "DeletedBy"                     UUID,

    -- Audit (AuditableEntity)
    "CreatedAt"                     TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),
    "CreatedBy"                     UUID            NOT NULL,
    "UpdatedAt"                     TIMESTAMPTZ,
    "UpdatedBy"                     UUID,

    CONSTRAINT "PK_Tenants" PRIMARY KEY ("Id")
);

-- Unique slug across platform (partial — excludes soft-deleted)
CREATE UNIQUE INDEX "UQ_Tenants_Slug"
    ON tenant."Tenants" ("Slug")
    WHERE "IsDeleted" = FALSE;

-- Filter for active-only queries (most reads)
CREATE INDEX "IX_Tenants_IsDeleted"
    ON tenant."Tenants" ("IsDeleted");

-- ============================================================================
-- SCHEMA: identity
-- Tables: users, user_credentials, refresh_tokens
-- Every user is owned by one tenant (TenantId).
-- ============================================================================

-- ---------------------------------------------------------------------------
-- identity.users
-- ---------------------------------------------------------------------------
CREATE TABLE identity."users" (
    "id"                UUID            NOT NULL    DEFAULT gen_random_uuid(),

    -- Tenant ownership — FK intentionally cross-schema (same DB); enforced
    -- at application layer via global query filter, not DB FK, so
    -- tenant can be deleted independently (soft-delete only).
    "tenant_id"         UUID            NOT NULL,

    -- Owned value-object: Email
    "email"             VARCHAR(320)    NOT NULL,

    -- Owned value-object: DisplayName
    "display_name"      VARCHAR(150)    NOT NULL,

    -- State
    "is_active"         BOOLEAN         NOT NULL    DEFAULT TRUE,
    "last_login_at"     TIMESTAMPTZ,

    -- GDPR right-to-erasure: pseudonymised marker replaces PII
    "anonymised_marker" VARCHAR(64),

    -- Soft-delete
    "is_deleted"        BOOLEAN         NOT NULL    DEFAULT FALSE,
    "deleted_at"        TIMESTAMPTZ,
    "deleted_by"        UUID,

    -- Audit
    "created_at"        TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),
    "created_by"        UUID            NOT NULL,
    "updated_at"        TIMESTAMPTZ,
    "updated_by"        UUID,

    CONSTRAINT "PK_users" PRIMARY KEY ("id")
);

-- Hot-path: tenant filtering (most queries are scoped to one tenant)
CREATE INDEX "ix_users_tenant_id"
    ON identity."users" ("tenant_id");

-- Unique email within a tenant (partial — excludes soft-deleted + anonymised)
CREATE UNIQUE INDEX "ix_users_tenant_email"
    ON identity."users" ("tenant_id", "email")
    WHERE "is_deleted" = FALSE AND "anonymised_marker" IS NULL;

-- Support is_deleted global query filter
CREATE INDEX "ix_users_tenant_is_deleted"
    ON identity."users" ("tenant_id", "is_deleted");

-- ---------------------------------------------------------------------------
-- identity.user_credentials
-- One credential record per user; rotated on password change.
-- Not tenant-scoped in isolation (user_id already implies tenant).
-- ---------------------------------------------------------------------------
CREATE TABLE identity."user_credentials" (
    "id"                    UUID            NOT NULL    DEFAULT gen_random_uuid(),
    "user_id"               UUID            NOT NULL,
    "tenant_id"             UUID            NOT NULL,   -- denormalised for query-filter

    -- PBKDF2-SHA512 hash + salt (base64-encoded)
    "password_hash"         VARCHAR(256)    NOT NULL,
    "password_salt"         VARCHAR(256)    NOT NULL,
    "password_updated_at"   TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),

    -- Lockout tracking
    "failed_login_attempts" INT             NOT NULL    DEFAULT 0,
    "locked_until"          TIMESTAMPTZ,

    CONSTRAINT "PK_user_credentials" PRIMARY KEY ("id")
);

-- One credential per user
CREATE UNIQUE INDEX "ix_user_credentials_user_id"
    ON identity."user_credentials" ("user_id");

-- ---------------------------------------------------------------------------
-- identity.refresh_tokens
-- Stored as SHA-256 hash; raw token returned to client, never stored.
-- ---------------------------------------------------------------------------
CREATE TABLE identity."refresh_tokens" (
    "id"            UUID            NOT NULL    DEFAULT gen_random_uuid(),
    "user_id"       UUID            NOT NULL,
    "tenant_id"     UUID            NOT NULL,   -- denormalised for query-filter

    -- SHA-256(rawToken), base64-encoded
    "token_hash"    VARCHAR(256)    NOT NULL,

    "expires_at"    TIMESTAMPTZ     NOT NULL,
    "is_revoked"    BOOLEAN         NOT NULL    DEFAULT FALSE,
    "revoked_at"    TIMESTAMPTZ,
    "revoked_reason" VARCHAR(256),

    "created_at"    TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),
    "created_by_ip" VARCHAR(64),

    CONSTRAINT "PK_refresh_tokens" PRIMARY KEY ("id")
);

-- Hot-path: token validation on every refresh
CREATE UNIQUE INDEX "ix_refresh_tokens_token_hash"
    ON identity."refresh_tokens" ("token_hash");

-- Bulk revocation on user logout
CREATE INDEX "ix_refresh_tokens_user_is_revoked"
    ON identity."refresh_tokens" ("user_id", "is_revoked");


-- =============================================================================
-- SCHEMA: rbac
-- Tables: Roles, Permissions, RolePermissions, UserRoleAssignments,
--         Scopes, ScopeHierarchy
-- All tables carry TenantId — global query filter enforced via EF Core.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- rbac.Roles
-- ---------------------------------------------------------------------------
CREATE TABLE rbac."Roles" (
    "Id"            UUID            NOT NULL    DEFAULT gen_random_uuid(),
    "TenantId"      UUID            NOT NULL,

    "Name"          VARCHAR(100)    NOT NULL,
    "Description"   VARCHAR(500),
    "IsSystemRole"  BOOLEAN         NOT NULL    DEFAULT FALSE,

    -- Soft-delete
    "IsDeleted"     BOOLEAN         NOT NULL    DEFAULT FALSE,
    "DeletedAt"     TIMESTAMPTZ,
    "DeletedBy"     UUID,

    -- Audit
    "CreatedAt"     TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),
    "CreatedBy"     UUID            NOT NULL,
    "UpdatedAt"     TIMESTAMPTZ,
    "UpdatedBy"     UUID,

    CONSTRAINT "PK_Roles" PRIMARY KEY ("Id")
);

-- Tenant isolation — most role queries are tenant-scoped
CREATE INDEX "IX_Roles_TenantId_IsDeleted"
    ON rbac."Roles" ("TenantId", "IsDeleted");

-- Unique role name within a tenant (partial — active roles only)
CREATE UNIQUE INDEX "UQ_Roles_TenantId_Name"
    ON rbac."Roles" ("TenantId", "Name")
    WHERE "IsDeleted" = FALSE;

-- ---------------------------------------------------------------------------
-- rbac.Permissions
-- ---------------------------------------------------------------------------
CREATE TABLE rbac."Permissions" (
    "Id"            UUID            NOT NULL    DEFAULT gen_random_uuid(),
    "TenantId"      UUID            NOT NULL,

    -- Owned value-object: PermissionCode (format: resource:action)
    "Code"          VARCHAR(100)    NOT NULL,

    "ResourceType"  VARCHAR(100)    NOT NULL,
    "Action"        VARCHAR(100)    NOT NULL,
    "Description"   VARCHAR(500),

    -- Soft-delete
    "IsDeleted"     BOOLEAN         NOT NULL    DEFAULT FALSE,
    "DeletedAt"     TIMESTAMPTZ,
    "DeletedBy"     UUID,

    -- Audit
    "CreatedAt"     TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),
    "CreatedBy"     UUID            NOT NULL,
    "UpdatedAt"     TIMESTAMPTZ,
    "UpdatedBy"     UUID,

    CONSTRAINT "PK_Permissions" PRIMARY KEY ("Id")
);

-- Unique code per tenant (partial — excludes soft-deleted)
CREATE UNIQUE INDEX "UQ_Permissions_TenantId_Code"
    ON rbac."Permissions" ("TenantId", "Code")
    WHERE "IsDeleted" = FALSE;

-- Global query filter support
CREATE INDEX "IX_Permissions_TenantId_IsDeleted"
    ON rbac."Permissions" ("TenantId", "IsDeleted");

-- ---------------------------------------------------------------------------
-- rbac.RolePermissions  (join table — no soft-delete; drop the row instead)
-- ---------------------------------------------------------------------------
CREATE TABLE rbac."RolePermissions" (
    "Id"                UUID        NOT NULL    DEFAULT gen_random_uuid(),
    "RoleId"            UUID        NOT NULL,
    "PermissionId"      UUID        NOT NULL,
    "TenantId"          UUID        NOT NULL,   -- denormalised for query-filter
    "GrantedByUserId"   UUID        NOT NULL,
    "GrantedAt"         TIMESTAMPTZ NOT NULL    DEFAULT NOW(),

    CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RolePermissions_Roles"
        FOREIGN KEY ("RoleId") REFERENCES rbac."Roles"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_RolePermissions_Permissions"
        FOREIGN KEY ("PermissionId") REFERENCES rbac."Permissions"("Id") ON DELETE CASCADE
);

-- Prevent duplicate grants; also primary lookup key
CREATE UNIQUE INDEX "UQ_RolePermissions_Role_Permission"
    ON rbac."RolePermissions" ("RoleId", "PermissionId");

-- Tenant filter on join table
CREATE INDEX "IX_RolePermissions_TenantId"
    ON rbac."RolePermissions" ("TenantId");

-- ---------------------------------------------------------------------------
-- rbac.UserRoleAssignments
-- Time-bounded (ExpiresAt nullable), soft-deactivated via IsActive.
-- ---------------------------------------------------------------------------
CREATE TABLE rbac."UserRoleAssignments" (
    "Id"                UUID            NOT NULL    DEFAULT gen_random_uuid(),
    "TenantId"          UUID            NOT NULL,
    "UserId"            UUID            NOT NULL,
    "RoleId"            UUID            NOT NULL,

    -- Null ScopeId = tenant-wide assignment
    "ScopeId"           UUID,

    "IsActive"          BOOLEAN         NOT NULL    DEFAULT TRUE,
    "ExpiresAt"         TIMESTAMPTZ,

    -- Deactivation audit
    "DeactivatedReason" VARCHAR(200),
    "DeactivatedAt"     TIMESTAMPTZ,

    -- Audit
    "CreatedAt"         TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),
    "CreatedBy"         UUID            NOT NULL,
    "UpdatedAt"         TIMESTAMPTZ,
    "UpdatedBy"         UUID,

    CONSTRAINT "PK_UserRoleAssignments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_UserRoleAssignments_Roles"
        FOREIGN KEY ("RoleId") REFERENCES rbac."Roles"("Id") ON DELETE RESTRICT
);

-- Hot-path: permission evaluation (user's active assignments in a scope)
CREATE INDEX "IX_UserRoleAssignments_TenantId_UserId_IsActive"
    ON rbac."UserRoleAssignments" ("TenantId", "UserId", "IsActive");

-- Hot-path: scope-qualified lookup (used in ScopeInheritanceStep)
CREATE INDEX "IX_UserRoleAssignments_TenantId_UserId_ScopeId_IsActive"
    ON rbac."UserRoleAssignments" ("TenantId", "UserId", "ScopeId", "IsActive");

-- Hot-path: cascade deactivation when a role is soft-deleted
CREATE INDEX "IX_UserRoleAssignments_TenantId_RoleId_IsActive"
    ON rbac."UserRoleAssignments" ("TenantId", "RoleId", "IsActive");

-- ---------------------------------------------------------------------------
-- rbac.Scopes
-- Hierarchical resource contexts (org → dept → project → custom).
-- ---------------------------------------------------------------------------
CREATE TABLE rbac."Scopes" (
    "Id"            UUID            NOT NULL    DEFAULT gen_random_uuid(),
    "TenantId"      UUID            NOT NULL,

    "Name"          VARCHAR(200)    NOT NULL,
    "Description"   VARCHAR(500),
    "Type"          INT             NOT NULL,   -- enum: 1=Org, 2=Dept, 3=Project, 4=Custom
    "ParentScopeId" UUID,                       -- nullable = root scope

    -- Soft-delete
    "IsDeleted"     BOOLEAN         NOT NULL    DEFAULT FALSE,
    "DeletedAt"     TIMESTAMPTZ,
    "DeletedBy"     UUID,

    -- Audit
    "CreatedAt"     TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),
    "CreatedBy"     UUID            NOT NULL,
    "UpdatedAt"     TIMESTAMPTZ,
    "UpdatedBy"     UUID,

    CONSTRAINT "PK_Scopes" PRIMARY KEY ("Id")
    -- ParentScopeId FK omitted intentionally: closure table is the authority
    -- for hierarchy; the adjacency column is kept only as a convenience for
    -- inserts and UI display.
);

CREATE INDEX "IX_Scopes_TenantId_IsDeleted"
    ON rbac."Scopes" ("TenantId", "IsDeleted");

-- ---------------------------------------------------------------------------
-- rbac.ScopeHierarchy  — Closure Table
--
-- WHY CLOSURE TABLE instead of ltree:
--   • Works on both PostgreSQL and Azure SQL without extensions
--   • Ancestor/descendant queries resolved with a single indexed join
--   • Depth column enables cheap max-depth enforcement for delegations
--   • O(n*d) storage (n=scopes, d=avg depth) — acceptable for enterprise
--     org structures (typically <5 levels deep)
--
-- Each scope insertion adds rows:
--   (self, self, 0)
--   (parent, self, 1) for each ancestor of parent, depth + 1
-- ---------------------------------------------------------------------------
CREATE TABLE rbac."ScopeHierarchy" (
    "Id"            UUID    NOT NULL    DEFAULT gen_random_uuid(),
    "TenantId"      UUID    NOT NULL,
    "AncestorId"    UUID    NOT NULL,
    "DescendantId"  UUID    NOT NULL,
    "Depth"         INT     NOT NULL,   -- 0 = self-reference

    CONSTRAINT "PK_ScopeHierarchy" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_ScopeHierarchy_Depth" CHECK ("Depth" >= 0)
);

-- No duplicate closure rows per tenant
CREATE UNIQUE INDEX "UQ_ScopeHierarchy_Ancestor_Descendant"
    ON rbac."ScopeHierarchy" ("TenantId", "AncestorId", "DescendantId");

-- Hot-path: find all ancestors of a scope (upward traversal — used in
-- ScopeInheritanceStep and GetEffectivePermissions)
CREATE INDEX "IX_ScopeHierarchy_TenantId_DescendantId"
    ON rbac."ScopeHierarchy" ("TenantId", "DescendantId", "Depth");

-- Hot-path: find all descendants (downward traversal — used by admin UI)
CREATE INDEX "IX_ScopeHierarchy_TenantId_AncestorId"
    ON rbac."ScopeHierarchy" ("TenantId", "AncestorId", "Depth");


-- =============================================================================
-- SCHEMA: policy
-- Tables: Policies
-- Stores JSON condition trees (AWS IAM-style) evaluated at runtime.
-- =============================================================================

CREATE TABLE policy."Policies" (
    "Id"                UUID            NOT NULL    DEFAULT gen_random_uuid(),
    "TenantId"          UUID            NOT NULL,

    "Name"              VARCHAR(200)    NOT NULL,
    "Description"       VARCHAR(1000),
    "Effect"            INT             NOT NULL,   -- enum: 1=Allow, 2=Deny

    -- JSON condition tree stored as JSONB for future operator-level indexing
    "ConditionTreeJson" JSONB           NOT NULL,

    -- Optional resource scope (NULL = global policy for all resources)
    "ResourceId"        UUID,

    -- Optional action filter (NULL = applies to all actions)
    "Action"            VARCHAR(100),

    "IsActive"          BOOLEAN         NOT NULL    DEFAULT TRUE,

    -- Soft-delete
    "IsDeleted"         BOOLEAN         NOT NULL    DEFAULT FALSE,
    "DeletedAt"         TIMESTAMPTZ,
    "DeletedBy"         UUID,

    -- Audit
    "CreatedAt"         TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),
    "CreatedBy"         UUID            NOT NULL,
    "UpdatedAt"         TIMESTAMPTZ,
    "UpdatedBy"         UUID,

    CONSTRAINT "PK_Policies" PRIMARY KEY ("Id")
);

-- Hot-path: fetching active policies for a tenant during CanUserAccess
-- (GlobalDenyStep + AbacPolicyStep load all active tenant policies)
CREATE INDEX "IX_Policies_TenantId_IsActive"
    ON policy."Policies" ("TenantId", "IsActive", "IsDeleted");

-- Hot-path: ResourceLevelOverrideStep — policies scoped to a specific resource
CREATE INDEX "IX_Policies_TenantId_ResourceId_IsActive"
    ON policy."Policies" ("TenantId", "ResourceId", "IsActive")
    WHERE "IsDeleted" = FALSE;

-- GIN index on condition tree JSON for potential future attribute-path queries
-- Disabled by default (enable when condition tree query complexity grows):
-- CREATE INDEX "IX_Policies_ConditionTree_Gin"
--     ON policy."Policies" USING GIN ("ConditionTreeJson");


-- =============================================================================
-- SCHEMA: delegation
-- Tables: Delegations
-- Time-bounded, depth-limited permission grants from one user to another.
-- =============================================================================

CREATE TABLE delegation."Delegations" (
    "Id"                UUID        NOT NULL    DEFAULT gen_random_uuid(),
    "TenantId"          UUID        NOT NULL,

    "DelegatorId"       UUID        NOT NULL,   -- user granting permissions
    "DelegateeId"       UUID        NOT NULL,   -- user receiving permissions

    -- PostgreSQL native text array; EF Core maps as string[]
    "PermissionCodes"   TEXT[]      NOT NULL,

    "ScopeId"           UUID        NOT NULL,

    -- Delegation validity window
    "ExpiresAt"         TIMESTAMPTZ NOT NULL,

    -- Chain depth (1 = direct, 2 = transitive once, max 3 platform limit)
    "ChainDepth"        INT         NOT NULL    DEFAULT 1,

    -- Revocation
    "IsRevoked"         BOOLEAN     NOT NULL    DEFAULT FALSE,
    "RevokedAt"         TIMESTAMPTZ,
    "RevokedByUserId"   UUID,

    -- Audit
    "CreatedAt"         TIMESTAMPTZ NOT NULL    DEFAULT NOW(),
    "CreatedBy"         UUID        NOT NULL,
    "UpdatedAt"         TIMESTAMPTZ,
    "UpdatedBy"         UUID,

    CONSTRAINT "PK_Delegations" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Delegations_NoSelfDelegation"
        CHECK ("DelegatorId" <> "DelegateeId"),
    CONSTRAINT "CK_Delegations_ChainDepth"
        CHECK ("ChainDepth" BETWEEN 1 AND 3)
);

-- Hot-path: DelegationCheckStep — find active delegation for a delegatee
-- in a given tenant (combined with ExpiresAt filter in EF global query filter)
CREATE INDEX "IX_Delegations_TenantId_DelegateeId_IsRevoked"
    ON delegation."Delegations" ("TenantId", "DelegateeId", "IsRevoked");

-- Hot-path: listing delegations created by a delegator (admin UI)
CREATE INDEX "IX_Delegations_TenantId_DelegatorId_IsRevoked"
    ON delegation."Delegations" ("TenantId", "DelegatorId", "IsRevoked");

-- Background / batch expiry scan (time-ordered per tenant)
CREATE INDEX "IX_Delegations_TenantId_ExpiresAt"
    ON delegation."Delegations" ("TenantId", "ExpiresAt")
    WHERE "IsRevoked" = FALSE;


-- =============================================================================
-- SCHEMA: audit
-- Tables: AuditLogs
-- Append-only. No UPDATE or DELETE permitted (enforced at application layer
-- via AuditDbContext.EnforceAppendOnly and DB-level trigger below).
-- =============================================================================

CREATE TABLE audit."AuditLogs" (
    "Id"                    UUID            NOT NULL    DEFAULT gen_random_uuid(),
    "TenantId"              UUID            NOT NULL,

    -- Log type: 1=AccessDecision, 2=AdminAction
    "LogType"               INT             NOT NULL,

    -- Distributed tracing linkage
    "CorrelationId"         UUID            NOT NULL,

    "ActorUserId"           UUID            NOT NULL,
    "Action"                VARCHAR(200)    NOT NULL,

    -- AccessDecision fields (nullable for AdminAction rows)
    "ResourceId"            UUID,
    "ScopeId"               UUID,
    "IsGranted"             BOOLEAN,
    "DenialReason"          VARCHAR(100),
    "CacheHit"              BOOLEAN,
    "EvaluationLatencyMs"   BIGINT,
    "PolicyId"              VARCHAR(100),

    -- Delegation chain summary (e.g. "delegatorId→delegateeId")
    "DelegationChain"       VARCHAR(500),

    -- AdminAction fields (nullable for AccessDecision rows)
    "TargetEntityType"      VARCHAR(100),
    "TargetEntityId"        UUID,
    "OldValue"              JSONB,
    "NewValue"              JSONB,

    -- Platform vs tenant action marker
    "IsPlatformAction"      BOOLEAN         NOT NULL    DEFAULT FALSE,

    "Timestamp"             TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),

    CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
);

-- ─── DB-level immutability guard ─────────────────────────────────────────────
-- Belt-and-suspenders: application layer already rejects UPDATE/DELETE via
-- EnforceAppendOnly(), but this trigger makes it database-enforced.
CREATE OR REPLACE FUNCTION audit.prevent_audit_log_mutation()
    RETURNS TRIGGER LANGUAGE plpgsql AS
$$
BEGIN
    RAISE EXCEPTION
        'AuditLogs are immutable. UPDATE and DELETE are forbidden. (TG_OP=%)', TG_OP;
END;
$$;

CREATE TRIGGER "trg_AuditLogs_NoUpdate"
    BEFORE UPDATE ON audit."AuditLogs"
    FOR EACH ROW EXECUTE FUNCTION audit.prevent_audit_log_mutation();

CREATE TRIGGER "trg_AuditLogs_NoDelete"
    BEFORE DELETE ON audit."AuditLogs"
    FOR EACH ROW EXECUTE FUNCTION audit.prevent_audit_log_mutation();

-- ─── Indexes ──────────────────────────────────────────────────────────────────
-- Primary time-range query (most common audit log query pattern)
CREATE INDEX "IX_AuditLogs_TenantId_Timestamp"
    ON audit."AuditLogs" ("TenantId", "Timestamp" DESC);

-- Filter by actor within a tenant + time window
CREATE INDEX "IX_AuditLogs_TenantId_ActorUserId_Timestamp"
    ON audit."AuditLogs" ("TenantId", "ActorUserId", "Timestamp" DESC);

-- Filter by resource within a tenant + time window
CREATE INDEX "IX_AuditLogs_TenantId_ResourceId_Timestamp"
    ON audit."AuditLogs" ("TenantId", "ResourceId", "Timestamp" DESC)
    WHERE "ResourceId" IS NOT NULL;

-- Distributed tracing lookup (correlationId → all log entries for a request)
CREATE INDEX "IX_AuditLogs_CorrelationId"
    ON audit."AuditLogs" ("CorrelationId");

-- Filter by log type for separation of access vs admin views
CREATE INDEX "IX_AuditLogs_TenantId_LogType_Timestamp"
    ON audit."AuditLogs" ("TenantId", "LogType", "Timestamp" DESC);


-- =============================================================================
-- EF CORE MIGRATION HISTORY TABLES
-- One per module, in its own schema — prevents migration conflicts when
-- modules are extracted into independent microservices.
-- =============================================================================

CREATE TABLE IF NOT EXISTS identity."__ef_migrations_identity" (
    "MigrationId"       VARCHAR(150)    NOT NULL,
    "ProductVersion"    VARCHAR(32)     NOT NULL,
    CONSTRAINT "PK___ef_migrations_identity" PRIMARY KEY ("MigrationId")
);

CREATE TABLE IF NOT EXISTS tenant."__ef_migrations_tenant" (
    "MigrationId"       VARCHAR(150)    NOT NULL,
    "ProductVersion"    VARCHAR(32)     NOT NULL,
    CONSTRAINT "PK___ef_migrations_tenant" PRIMARY KEY ("MigrationId")
);

CREATE TABLE IF NOT EXISTS rbac."__ef_migrations_rbac" (
    "MigrationId"       VARCHAR(150)    NOT NULL,
    "ProductVersion"    VARCHAR(32)     NOT NULL,
    CONSTRAINT "PK___ef_migrations_rbac" PRIMARY KEY ("MigrationId")
);

CREATE TABLE IF NOT EXISTS policy."__ef_migrations_policy" (
    "MigrationId"       VARCHAR(150)    NOT NULL,
    "ProductVersion"    VARCHAR(32)     NOT NULL,
    CONSTRAINT "PK___ef_migrations_policy" PRIMARY KEY ("MigrationId")
);

CREATE TABLE IF NOT EXISTS delegation."__ef_migrations_delegation" (
    "MigrationId"       VARCHAR(150)    NOT NULL,
    "ProductVersion"    VARCHAR(32)     NOT NULL,
    CONSTRAINT "PK___ef_migrations_delegation" PRIMARY KEY ("MigrationId")
);

CREATE TABLE IF NOT EXISTS audit."__ef_migrations_audit" (
    "MigrationId"       VARCHAR(150)    NOT NULL,
    "ProductVersion"    VARCHAR(32)     NOT NULL,
    CONSTRAINT "PK___ef_migrations_audit" PRIMARY KEY ("MigrationId")
);


-- =============================================================================
-- ROW LEVEL SECURITY (optional hardening — recommended for production)
-- Adds a second enforcement layer for multi-tenancy on top of EF query filters.
-- Enable per-table after confirming application-layer correctness.
-- =============================================================================

-- Example for identity.users (replicate pattern for all tenant-scoped tables):
-- ALTER TABLE identity."users" ENABLE ROW LEVEL SECURITY;
-- ALTER TABLE identity."users" FORCE ROW LEVEL SECURITY;
--
-- CREATE POLICY "tenant_isolation" ON identity."users"
--     USING (
--         "tenant_id" = current_setting('app.current_tenant_id')::UUID
--         OR current_setting('app.is_super_admin', TRUE) = 'true'
--     );
--
-- Application must call: SET LOCAL "app.current_tenant_id" = '<tid>';
-- at the start of each transaction (handled in ITenantContext middleware).


-- =============================================================================
-- DATA RETENTION — NOTES (enforced by scheduled jobs, not DDL)
-- =============================================================================
-- Audit logs:       7 years  → archive to cold storage (S3/Azure Blob) after
--                              365 days, keep index rows in audit."AuditLogs"
--                              with "IsPlatformAction" marker.
-- Soft-deleted roles/perms: 2 years before physical archival.
-- GDPR erasure:     anonymised_marker on identity."users" replaces email +
--                              display_name; audit log ActorUserId retained
--                              as pseudonym — no physical audit row deletion.
-- Delegation records:  retain 2 years post-expiry for audit trail.


-- =============================================================================
-- MULTI-TENANCY FILTER COVERAGE — VERIFICATION CHECKLIST
-- =============================================================================
-- Table                              TenantId?   EF Global Filter?
-- ─────────────────────────────────────────────────────────────────
-- tenant."Tenants"                   ✗           N/A (IS the registry)
-- identity."users"                   ✓           IdentityDbContext
-- identity."user_credentials"        ✓           IdentityDbContext
-- identity."refresh_tokens"          ✓           IdentityDbContext
-- rbac."Roles"                       ✓           RbacDbContext
-- rbac."Permissions"                 ✓           RbacDbContext
-- rbac."RolePermissions"             ✓           RbacDbContext
-- rbac."UserRoleAssignments"         ✓           RbacDbContext
-- rbac."Scopes"                      ✓           RbacDbContext
-- rbac."ScopeHierarchy"              ✓           RbacDbContext
-- policy."Policies"                  ✓           PolicyDbContext
-- delegation."Delegations"           ✓           DelegationDbContext
-- audit."AuditLogs"                  ✓           AuditDbContext
-- =============================================================================
