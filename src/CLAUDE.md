# CLAUDE.md — Enterprise RBAC System

> **Role:** You are a senior software architect and security expert with deep experience building enterprise IAM systems (Microsoft Azure RBAC, AWS IAM calibre). Every decision you make must be production-grade, security-first, and implementation-ready. Never produce summaries — produce the actual design artifact, code signature, SQL DDL, or configuration a senior developer can use directly.

---

## Project Overview

A **production-grade Modular Monolith RBAC system** built with Clean Architecture, designed to evolve into microservices. It supports multi-tenancy, RBAC, scoped hierarchy, ABAC, a JSON policy engine, time-bound delegation, and full auditability.

**North Star:** Every feature decision must map to one of the 43 core requirements below. If a design choice cannot be traced to a requirement, question it.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Backend | .NET 8 Web API, Clean Architecture, MediatR (CQRS) |
| Database (primary) | PostgreSQL 16 |
| Database (supported) | Azure SQL |
| ORM | EF Core (primary) + Dapper (read-heavy hot paths) |
| Cache | Redis 7 (mandatory — never optional in production) |
| Auth | JWT + OAuth2 / OpenID Connect |
| Frontend | React + TypeScript |
| Messaging (future) | Kafka / Azure Service Bus |
| Tracing | OpenTelemetry → Jaeger (dev) / Azure Monitor (prod) |
| Metrics | Prometheus + Grafana |
| Logging | Serilog (JSON sink) |
| Testing | xUnit + FluentAssertions |

---

## Architecture Style

**Modular Monolith with Clean Architecture.** Each module is independently extractable into a microservice without changing its internal contracts.

### Layer Order (strict — no skipping)

```
Domain → Application → Infrastructure → API
```

### Module Boundaries (hard rules)

- No module may reference another module's `Infrastructure` or `Domain` project directly.
- Cross-module data reads use **read models / projections**, never JOIN across module DB tables.
- Modules communicate via: **(a)** published service interfaces or **(b)** domain events via in-process MediatR notifications (swap to Kafka/Service Bus later without changing publishers).

---

## Solution Structure

```
/src
  /Modules
    /Identity
      Identity.Domain
      Identity.Application
      Identity.Infrastructure
      Identity.API
    /TenantManagement
      TenantManagement.Domain
      TenantManagement.Application
      TenantManagement.Infrastructure
      TenantManagement.API
    /RbacCore
      RbacCore.Domain
      RbacCore.Application
      RbacCore.Infrastructure
      RbacCore.API
    /PermissionEngine
      PermissionEngine.Domain
      PermissionEngine.Application
      PermissionEngine.Infrastructure
      PermissionEngine.API
    /PolicyEngine
      PolicyEngine.Domain
      PolicyEngine.Application
      PolicyEngine.Infrastructure
      PolicyEngine.API
    /Delegation
      Delegation.Domain
      Delegation.Application
      Delegation.Infrastructure
      Delegation.API
    /Audit
      Audit.Domain
      Audit.Application
      Audit.Infrastructure
      Audit.API
  /SharedKernel
    SharedKernel.Domain          ← base entity, aggregate root, domain event
    SharedKernel.Application     ← base CQRS interfaces, ICurrentTenantContext
    SharedKernel.Infrastructure  ← EF base context, Redis client, outbox pattern
  /ApiGateway                    ← single entry-point API project
```

---

## Module Contracts

For each module, always define and maintain:

**(a) Public service interfaces** exposed to other modules
**(b) Domain events emitted** — name + payload shape
**(c) Data owned exclusively** vs. data read from other modules
**(d) Anti-corruption layer** used when reading cross-module data

### Required Domain Events (minimum)

Every event must carry `correlationId`, `tenantId`, `occurredAt`, and event-specific payload.

| Event | Payload Fields |
|---|---|
| `UserCreated` | userId, email, tenantId |
| `UserRoleAssigned` | userId, roleId, scopeId, assignedBy |
| `UserRoleRevoked` | userId, roleId, scopeId, revokedBy |
| `RoleCreated` | roleId, tenantId, name, createdBy |
| `RoleDeleted` | roleId, tenantId, deletedBy |
| `PermissionGranted` | permissionId, roleId, tenantId, grantedBy |
| `PermissionRevoked` | permissionId, roleId, tenantId, revokedBy |
| `PolicyCreated` | policyId, tenantId, name, createdBy |
| `PolicyUpdated` | policyId, tenantId, updatedBy, changeSet |
| `PolicyDeleted` | policyId, tenantId, deletedBy |
| `DelegationCreated` | delegationId, delegatorId, delegateeId, permissions[], expiresAt |
| `DelegationRevoked` | delegationId, revokedBy, reason |
| `DelegationExpired` | delegationId, delegatorId, delegateeId |
| `TenantCreated` | tenantId, name, adminUserId |
| `TenantSuspended` | tenantId, suspendedBy, reason |

---

## Multi-Tenancy Rules

- **Isolation model:** Row-level isolation — shared schema, `TenantId` on every tenant-scoped table.
- **Enforcement:** Global EF Core query filters inject `TenantId` from the ambient `ICurrentTenantContext`. No query may rely on developers manually adding `WHERE TenantId = ?`. Violation = bug.
- **Super-admin role:** Operates outside tenant isolation. Claims must include `is_super_admin: true`. All super-admin actions are audited with a `SuperAdminAction` marker. Endpoint handlers must explicitly opt-in to bypassing tenant filters — this is never the default.
- **Tenant bootstrapping (chicken-and-egg solution):** On `TenantCreated` event, a `TenantBootstrapper` service runs in a system-level context (bypassing tenant filter) to seed: default admin user, `tenant-admin` role with full tenant permissions, and default permission templates. This runs atomically in a single transaction before the event is published to the rest of the system.

### Tradeoffs vs. Other Isolation Models

| Model | Pro | Con |
|---|---|---|
| Row-level (chosen) | Cheap ops, single schema | Cross-tenant data leak risk if filter missed |
| Schema-per-tenant | Strong isolation, easy restore | High schema count, migration complexity |
| DB-per-tenant | Maximum isolation | Cost, connection pool explosion |

---

## Data Lifecycle Rules (non-negotiable)

1. **Soft delete only.** Roles, permissions, assignments — never physically deleted. Use `DeletedAt` timestamp. Global EF filter excludes `DeletedAt IS NOT NULL` by default.
2. **Cascade on role soft-delete.** When a role is soft-deleted, all `RoleAssignments` referencing it are automatically deactivated with `DeactivatedReason = "RoleDeleted"`.
3. **Audit logs are immutable.** No UPDATE or DELETE on audit records. Ever. Archive to cold storage after retention period.
4. **Retention periods:**
   - Audit logs: 7 years
   - Soft-deleted roles/permissions: 2 years before archival
5. **GDPR right-to-erasure:** Pseudonymise `ActorId` in audit logs (replace with a deterministic hash) rather than deleting records. Store the mapping table separately with actual deletion on erasure request.

---

## Database Schema (key tables)

Every table that is tenant-scoped **must** have a `TenantId UUID NOT NULL` column with a covering index. The following is the canonical column set — always extend, never reduce.

### Tenants
```sql
Id UUID PK, Name VARCHAR(255) NOT NULL, Slug VARCHAR(100) UNIQUE NOT NULL,
Status VARCHAR(50) NOT NULL DEFAULT 'Active',
Settings JSONB, CreatedAt TIMESTAMPTZ NOT NULL, UpdatedAt TIMESTAMPTZ,
DeletedAt TIMESTAMPTZ, IsDeleted BOOLEAN NOT NULL DEFAULT FALSE
```

### Users
```sql
Id UUID PK, TenantId UUID NOT NULL FK→Tenants,
Email VARCHAR(320) NOT NULL, DisplayName VARCHAR(255),
PasswordHash VARCHAR(512), ExternalIdpId VARCHAR(512),
IsActive BOOLEAN NOT NULL DEFAULT TRUE,
CreatedAt TIMESTAMPTZ NOT NULL, DeletedAt TIMESTAMPTZ, IsDeleted BOOLEAN NOT NULL DEFAULT FALSE
-- INDEX: (TenantId, Email) UNIQUE WHERE IsDeleted = FALSE
-- INDEX: (TenantId) for tenant-scoped queries
```

### Roles
```sql
Id UUID PK, TenantId UUID NOT NULL FK→Tenants,
Name VARCHAR(255) NOT NULL, Description TEXT, IsSystem BOOLEAN NOT NULL DEFAULT FALSE,
CreatedAt TIMESTAMPTZ NOT NULL, UpdatedAt TIMESTAMPTZ,
DeletedAt TIMESTAMPTZ, IsDeleted BOOLEAN NOT NULL DEFAULT FALSE
-- UNIQUE: (TenantId, Name) WHERE IsDeleted = FALSE
```

### Permissions
```sql
Id UUID PK, TenantId UUID NOT NULL FK→Tenants,
Action VARCHAR(255) NOT NULL, ResourceType VARCHAR(255) NOT NULL,
Description TEXT, CreatedAt TIMESTAMPTZ NOT NULL,
DeletedAt TIMESTAMPTZ, IsDeleted BOOLEAN NOT NULL DEFAULT FALSE
-- UNIQUE: (TenantId, Action, ResourceType) WHERE IsDeleted = FALSE
```

### RolePermissions
```sql
Id UUID PK, RoleId UUID NOT NULL FK→Roles, PermissionId UUID NOT NULL FK→Permissions,
GrantedAt TIMESTAMPTZ NOT NULL, GrantedBy UUID NOT NULL,
DeletedAt TIMESTAMPTZ, IsDeleted BOOLEAN NOT NULL DEFAULT FALSE
-- UNIQUE: (RoleId, PermissionId) WHERE IsDeleted = FALSE
-- INDEX: (RoleId) for hot-path permission lookups
```

### UserRoleAssignments
```sql
Id UUID PK, TenantId UUID NOT NULL FK→Tenants,
UserId UUID NOT NULL FK→Users, RoleId UUID NOT NULL FK→Roles,
ScopeId UUID FK→Scopes,
AssignedAt TIMESTAMPTZ NOT NULL, AssignedBy UUID NOT NULL,
ExpiresAt TIMESTAMPTZ,
IsActive BOOLEAN NOT NULL DEFAULT TRUE,
DeactivatedReason VARCHAR(255),
DeletedAt TIMESTAMPTZ, IsDeleted BOOLEAN NOT NULL DEFAULT FALSE
-- INDEX: (TenantId, UserId) — primary hot path
-- INDEX: (TenantId, RoleId) — for role-deletion cascade
```

### Scopes
```sql
Id UUID PK, TenantId UUID NOT NULL FK→Tenants,
Name VARCHAR(255) NOT NULL, Type VARCHAR(50) NOT NULL, -- 'Organization'|'Department'|'Project'
ParentId UUID FK→Scopes (self-referencing),
CreatedAt TIMESTAMPTZ NOT NULL
-- INDEX: (TenantId, ParentId)
```

### ScopeHierarchy (closure table)
```sql
AncestorId UUID NOT NULL FK→Scopes,
DescendantId UUID NOT NULL FK→Scopes,
Depth INT NOT NULL,
PRIMARY KEY (AncestorId, DescendantId)
-- Chosen over ltree: closure table is database-agnostic (PostgreSQL + Azure SQL), queries are pure SQL joins, depth is explicit
-- INDEX: (DescendantId) — for "find all ancestors" traversal
```

### Policies
```sql
Id UUID PK, TenantId UUID NOT NULL FK→Tenants,
Name VARCHAR(255) NOT NULL, Description TEXT,
Effect VARCHAR(10) NOT NULL, -- 'Allow'|'Deny'
ConditionTree JSONB NOT NULL,
IsActive BOOLEAN NOT NULL DEFAULT TRUE,
CreatedAt TIMESTAMPTZ NOT NULL, UpdatedAt TIMESTAMPTZ,
DeletedAt TIMESTAMPTZ, IsDeleted BOOLEAN NOT NULL DEFAULT FALSE
-- INDEX: (TenantId, IsActive) — policy evaluation hot path
```

### Delegations
```sql
Id UUID PK, TenantId UUID NOT NULL FK→Tenants,
DelegatorId UUID NOT NULL FK→Users, DelegateeId UUID NOT NULL FK→Users,
PermissionIds UUID[] NOT NULL,
ScopeId UUID FK→Scopes,
CreatedAt TIMESTAMPTZ NOT NULL, ExpiresAt TIMESTAMPTZ NOT NULL,
RevokedAt TIMESTAMPTZ, RevokedBy UUID,
ChainDepth INT NOT NULL DEFAULT 0,
Status VARCHAR(50) NOT NULL DEFAULT 'Active' -- 'Active'|'Revoked'|'Expired'
-- INDEX: (TenantId, DelegateeId, Status, ExpiresAt) — eval hot path
-- INDEX: (TenantId, DelegatorId) — for over-delegation checks
```

### AuditLogs
```sql
Id UUID PK, TenantId UUID NOT NULL,
ActorId UUID NOT NULL, -- pseudonymised on GDPR erasure
Action VARCHAR(255) NOT NULL, ResourceType VARCHAR(255),
ResourceId UUID, ScopeId UUID,
Result VARCHAR(50) NOT NULL, -- 'Granted'|'Denied'
DeniedReason VARCHAR(255),
Metadata JSONB, -- full AccessResult, delegation chain, evaluated policies
CorrelationId UUID NOT NULL,
CreatedAt TIMESTAMPTZ NOT NULL
-- INDEX: (TenantId, CreatedAt DESC) — time-range queries
-- INDEX: (TenantId, ActorId, CreatedAt DESC) — user audit trail
-- INDEX: (TenantId, ResourceId) — resource audit trail
-- NO soft delete. NO update. Immutable.
```

---

## Permission Evaluation Engine

### Core Method Signature

```csharp
Task<AccessResult> CanUserAccess(
    UserId userId,
    string action,
    ResourceId resourceId,
    ScopeId scopeId,
    EvaluationContext context
);

public record EvaluationContext(
    TenantId TenantId,
    DelegationChain? DelegationChain,
    UserAttributes UserAttributes,
    ResourceAttributes ResourceAttributes,
    EnvironmentAttributes EnvironmentAttributes, // time, IP, device
    CorrelationId CorrelationId
);

public record AccessResult(
    AccessDecision Decision,          // Granted | Denied
    DeniedReason? DeniedReason,
    IReadOnlyList<EvaluatedPolicy> EvaluatedPolicies,
    IReadOnlyList<EffectiveRole> EffectiveRoles,
    DelegationChain? DelegationChainUsed,
    bool CacheHit,
    long EvaluationLatencyMs
);
```

### Evaluation Pipeline (strict precedence — implement in this exact order)

```
Step 1: EXPLICIT GLOBAL DENY
  → Any active policy on tenant/resource returns unconditional DENY?
  → YES: return Denied(ExplicitGlobalDeny). Short-circuit. Do not evaluate further.

Step 2: RESOURCE-LEVEL OVERRIDE
  → Direct grant or deny scoped to exact ResourceId?
  → Explicit DENY: return Denied(ResourceLevelDeny). Short-circuit.
  → Explicit ALLOW: continue to Step 3 (do not return yet).

Step 3: DELEGATION CHECK
  → Active, non-expired delegation for this user on this action+scope?
  → Validate: (a) delegator still holds the permission, (b) not revoked,
              (c) chain depth ≤ tenant-configured max (default: 1, hard platform max: 3)
  → Valid: continue evaluation using delegator's effective permissions.
           Record delegation chain in audit.
  → Invalid/expired: skip delegation path, continue as the user's own permissions.

Step 4: SCOPE INHERITANCE RESOLUTION
  → Walk ScopeHierarchy closure table upward: project → department → organization.
  → Collect all roles granted at any ancestor scope.
  → A lower-scope (more specific) explicit deny overrides an inherited allow from higher scope.
  → Build EffectiveRoles[] list.

Step 5: ABAC POLICY EVALUATION
  → Evaluate all applicable JSON condition tree policies independently.
  → Any policy returns DENY: return Denied(AbacPolicyDeny). Short-circuit.
  → All return ALLOW (or no applicable policies): continue.

Step 6: ROLE-BASED PERMISSION CHECK
  → Check EffectiveRoles[] from Step 4 for the requested action on resource type.
  → Deny-overrides-allow: if user holds both permitting and denying role for same action, DENY wins.
  → At least one ALLOW and no DENY: return Granted.

Step 7: DEFAULT DENY
  → No rule granted access. Return Denied(DefaultDeny).
```

### Conflict Resolution (apply consistently across all steps)

- Explicit DENY always beats explicit ALLOW at the same precedence level.
- Lower scope (more specific) overrides inherited permission from higher scope.
- Among roles at the same scope level with conflicting permissions: DENY wins.
- Among policies at the same step: first DENY short-circuits (first-deny wins).

---

## JWT Token Design

### Payload Schema

```json
{
  "sub": "<user-uuid>",
  "tid": "<tenant-uuid>",
  "email": "user@example.com",
  "roles": ["<role-id-1>", "<role-id-2>"],
  "scp": ["org:<uuid>", "dept:<uuid>"],
  "del": "<delegator-user-uuid> | null",
  "del_chain": ["<uuid1>", "<uuid2>"],
  "is_super_admin": false,
  "tv": 42,
  "jti": "<token-uuid>",
  "iat": 1700000000,
  "exp": 1700000900
}
```

- `tv` = token version. Must match Redis key `token-version:{userId}`.
- Access token TTL: **15 minutes**
- Refresh token TTL: **7 days, sliding**

### Token Version Invalidation

Any of these events must **atomically increment** `token-version:{userId}` in Redis:

- `UserRoleAssigned`
- `UserRoleRevoked`
- `DelegationCreated`
- `DelegationRevoked`

### Failure Handling (implement all cases)

| Condition | Response |
|---|---|
| Corrupt / malformed JWT | `401` — log IP, user agent, SHA256 hash of raw token (never the token itself) |
| Expired access + valid refresh | Issue new access token silently |
| Expired refresh | `401` — redirect to login |
| Stale `tv` claim | `401` — invalidate refresh token, force re-login |
| Super-admin on tenant endpoint with missing `tid` | Inject system tenant context |

---

## Caching Architecture

### Cache Key Schemas

```
perm:{tenantId}:{userId}:{action}:{resourceType}:{scopeId}   TTL: 60s
roles:{tenantId}:{userId}                                      TTL: 300s
policy:{tenantId}:{policyId}                                   TTL: 600s
token-version:{userId}                                         TTL: sliding 7d
scope-tree:{tenantId}                                          TTL: 3600s
delegation:{tenantId}:{userId}                                 TTL: 60s
```

### Cache Eviction Triggers

| Domain Event | Keys to Bust |
|---|---|
| `UserRoleAssigned` / `UserRoleRevoked` | `perm:{tid}:{uid}:*`, `roles:{tid}:{uid}`, increment `token-version:{uid}` |
| `PolicyCreated` / `PolicyUpdated` / `PolicyDeleted` | `perm:{tid}:*`, `policy:{tid}:{policyId}` |
| `RoleDeleted` | `perm:{tid}:*`, `roles:{tid}:*` for all users with that role |
| `DelegationCreated` / `DelegationRevoked` / `DelegationExpired` | `perm:{tid}:{uid}:*`, `delegation:{tid}:{uid}`, increment `token-version:{uid}` |
| `ScopeUpdated` | `scope-tree:{tid}`, `perm:{tid}:*` |
| `TenantSuspended` | All keys matching `*:{tid}:*` |

### Stampede Protection

Use Redis `SET NX` lock: first request past expiry acquires a 2-second lock and recomputes. All others serve the stale value during recompute. Alternatively, apply PER (probabilistic early expiration): when remaining TTL < 10% of original TTL under high traffic, probabilistically refresh before expiry.

### Distributed Invalidation

On cache bust events, publish `InvalidateCache` to Redis pub/sub channel `cache-invalidation:{tenantId}`. All app instances subscribe and bust their local L1 in-memory cache. Redis (L2) handles itself.

---

## Failure Modes & Resilience

| Failure | Behaviour |
|---|---|
| Redis unavailable | Fall through to DB for eval. Log degraded mode. **Configurable per deployment:** deny-all (secure) vs. allow-through (available). Default: allow-through. Document this decision explicitly. |
| Eval timeout > 200ms | Return `Denied`. Log full context. Alert if rate > 1% of requests. |
| Partial policy failure (one rule errors) | Skip errored rule, log warning, continue. If > 50% of applicable policies error in one request: fail-closed. |
| Corrupt JWT | `401`. Log IP, user agent, token hash. Never log raw token. |
| DB unavailable during eval | `503 Service Unavailable` — **not** `403`. Caller must distinguish infra failure from permission denial. |

---

## API Design

### Versioning

- URL path versioning: `/api/v1/`, `/api/v2/`
- N-1 compatibility: v1 deprecated but supported for 6 months after v2 ships
- Breaking changes → version bump. Additive changes (new optional fields) → no bump.
- Communicate via `Deprecation` and `Sunset` response headers.

### Endpoint Inventory

```
POST   /api/v1/tenants
GET    /api/v1/tenants/{id}
PUT    /api/v1/tenants/{id}
DELETE /api/v1/tenants/{id}                              ← soft delete

POST   /api/v1/tenants/{tid}/users
GET    /api/v1/tenants/{tid}/users/{uid}
PUT    /api/v1/tenants/{tid}/users/{uid}

POST   /api/v1/tenants/{tid}/roles
GET    /api/v1/tenants/{tid}/roles
PUT    /api/v1/tenants/{tid}/roles/{rid}
DELETE /api/v1/tenants/{tid}/roles/{rid}                 ← soft delete + cascade deactivation

POST   /api/v1/tenants/{tid}/permissions
GET    /api/v1/tenants/{tid}/roles/{rid}/permissions
POST   /api/v1/tenants/{tid}/roles/{rid}/permissions/{pid}   ← assign
DELETE /api/v1/tenants/{tid}/roles/{rid}/permissions/{pid}   ← revoke

POST   /api/v1/tenants/{tid}/users/{uid}/roles           ← assign role + scope
DELETE /api/v1/tenants/{tid}/users/{uid}/roles/{rid}

POST   /api/v1/tenants/{tid}/policies
GET    /api/v1/tenants/{tid}/policies/{pid}
PUT    /api/v1/tenants/{tid}/policies/{pid}
DELETE /api/v1/tenants/{tid}/policies/{pid}

POST   /api/v1/tenants/{tid}/delegations
GET    /api/v1/tenants/{tid}/delegations/{did}
DELETE /api/v1/tenants/{tid}/delegations/{did}           ← early revoke

POST   /api/v1/tenants/{tid}/permissions/check           ← full AccessResult response
GET    /api/v1/tenants/{tid}/audit-logs?from=&to=&userId=&action=&resourceId=&page=&pageSize=
                                                          ← Accept: text/csv for export
```

### Security Hardening (enforce on every endpoint)

1. Every `{tid}` endpoint validates `tid` matches the `tid` JWT claim. Mismatch → `403`.
2. Privilege escalation prevention: a user may not assign a role granting more permissions than they hold. Validate at assignment time via `CanUserAccess`.
3. Rate limiting: per-user and per-tenant.
4. Input validation via FluentValidation on all commands.
5. No stack traces in error responses (production).
6. Required response headers: `X-Content-Type-Options`, `X-Frame-Options`, `Strict-Transport-Security`.

---

## Delegation Rules

- **Default chain max = 1** (a delegatee may not re-delegate). Configurable per tenant. Hard platform limit = 3.
- **Over-delegation prevention:** validate at creation time that the delegator holds every permission being delegated. If the delegator later loses a permission, the delegation becomes ineffective — evaluated at check time, not creation time.
- **Acting-on-behalf audit:** every `AccessResult` via delegation must record `ActingOnBehalfOf: { delegatorId, delegationId, chainDepth }` in the audit log.
- **Early revocation:** `DELETE /delegations/{did}` — immediately marks revoked, busts `delegation:{tid}:{uid}` cache, increments `token-version:{uid}`, emits `DelegationRevoked`.
- **Expiry is checked at evaluation time**, not via background job. No in-flight request may use an expired delegation regardless of cache state.

---

## Observability

### Structured Log Fields (every `CanUserAccess` call)

```
correlationId, tenantId, userId, action, resourceId, scopeId,
result, deniedReason, cacheHit, evaluationLatencyMs,
delegationChainUsed, policiesEvaluated[], timestamp
```

Use Serilog JSON sink. **Never log:** raw JWT values, passwords, PII beyond userId.

### OpenTelemetry Spans

Create spans for: inbound request, cache lookup, DB query, each policy evaluation step, cache write. Propagate `traceparent` / `tracestate`.

### Prometheus Metrics

```
rbac_eval_total{result, cache_hit, tenant_id}             ← counter
rbac_eval_duration_ms{quantile}                            ← histogram
  P50 target: <5ms (cache hit), <50ms (cache miss)
  P95 target: <10ms (cache hit), <100ms (cache miss)
  P99 target: <20ms (cache hit), <200ms (cache miss)
rbac_active_delegations{tenant_id}                         ← gauge
rbac_cache_evictions_total{key_type}                       ← counter
rbac_policy_eval_errors_total                              ← counter
```

### Alerting Rules

| Alert | Condition |
|---|---|
| High denial rate | Denial rate > 10% of requests over 5 minutes |
| Slow evaluation | P99 `rbac_eval_duration_ms` > 200ms |
| Policy eval errors | `rbac_policy_eval_errors_total` > 0 for 2 minutes |
| Redis down | Redis connectivity lost |
| Deep delegation chain | Chain depth > configured max detected |

---

## Frontend (Admin Panel)

### Component Tree

- **RoleEditor** — create/edit role, permission checkbox matrix, scope selector
- **PermissionMatrixView** — read-only grid: roles × permissions with scope filter
- **UserRoleAssignment** — user search, role multi-select, scope picker, effective/expiry dates
- **PolicyBuilder** — visual JSON condition tree composer, preview JSON, test against sample context
- **DelegationManager** — create delegation (delegatee, permissions subset, date range), list active, revoke with confirmation
- **AuditLogViewer** — filterable table (date range, user, action, resource, result), row expand for full `AccessResult`, CSV export
- **TenantDashboard** — health summary: active users, roles, policies, recent denial rate chart

### UI-Level RBAC Enforcement

- Use `AbilityContext` (React context) that calls `POST /api/v1/permissions/check` on load.
- Cache results for the session.
- Wrap all mutation controls in `<Authorized action="..." resource="...">` — disables or hides based on check result.
- A `role:viewer` sees controls but cannot submit mutations.
- A `role:permission-manager` can manage permissions but not create tenants.

### Real-Time Updates

Use SignalR WebSocket. On role/policy change in backend, push `rbac:invalidated` event to all connected admin panel clients for that tenant. Client refetches affected data and re-evaluates cached permission checks on receipt.

---

## Testing Requirements

### Unit Tests (minimum coverage)

- Every domain model: test all invariants (e.g. `Role` cannot have empty name, `Delegation` cannot have `ExpiresAt` in the past).
- `CanUserAccess` pipeline: minimum **20 test cases** covering every branch:
  - Cache hit (Granted)
  - Cache hit (Denied)
  - Each DeniedReason enum value
  - Delegation valid
  - Delegation expired
  - Delegation chain depth exceeded
  - Scope inheritance (role from ancestor scope)
  - Scope-level deny overrides inherited allow
  - ABAC policy ALLOW
  - ABAC policy DENY
  - Conflict resolution: deny-overrides-allow at same level
  - Super-admin bypass
  - Cross-tenant rejection
  - Default deny (no matching rule)

### Test Rules

- xUnit + FluentAssertions.
- **No mocking of domain logic.** Only mock infrastructure (Redis, DB).
- Security test suite must explicitly cover privilege escalation prevention.

---

## Security Tenant Isolation: Three-Layer Enforcement

| Layer | Mechanism |
|---|---|
| API | Validate `{tid}` matches `tid` JWT claim on every request |
| Infrastructure | Global EF Core query filter injects `TenantId` from `ICurrentTenantContext` |
| Application | Explicit `tenantId` parameter validation in every command/query handler |

All three layers must pass. Bypass at any layer = security bug.

---

## Infrastructure (docker-compose for local dev)

```yaml
# Required services — always run all of them locally
services:
  postgres:     postgres:16-alpine   ← primary DB, port 5432
  redis:        redis:7-alpine       ← with --appendonly yes (persistence)
  jaeger:       jaegertracing/all-in-one:latest   ← tracing UI port 16686
  prometheus:   prom/prometheus:latest             ← metrics scraper
  grafana:      grafana/grafana:latest             ← dashboards port 3000
```

DB initialization: run EF Core migrations on startup via a `DbMigrator` hosted service. Use `DataSeeder` for idempotent seed data (default roles, permissions, system tenant).

---

## Migration Strategy

- EF Core migrations exclusively for schema changes.
- **Never modify an existing migration** — always add a new one.
- **Zero-downtime pattern (expand-contract):** for breaking changes:
  1. Add new column (nullable)
  2. Backfill data
  3. Add constraint
  4. Remove old column
  — Each step is a separate deployment.
- Seed data via `DataSeeder` runs idempotently on every startup.

---

## Secrets Management

- All secrets (DB connection strings, JWT signing keys, Redis passwords) via environment variables or Azure Key Vault.
- **Never** committed to source code or `appsettings.json` in version control.
- `appsettings.Development.json` may use local dev values only — excluded from VCS via `.gitignore`.

---

## Development Workflow Rules

1. **Implement phases sequentially** — Phase 1 architecture decisions are locked before Phase 2 schema work begins. Schema is locked before Phase 3 engine work begins. Do not prototype ahead.
2. **Every PR must include:** the feature code, corresponding unit tests, any required EF Core migration, and cache eviction registration for any new domain events.
3. **Before any cross-module data access:** define the read model interface first, get it reviewed, then implement.
4. **Performance budget for `CanUserAccess`:** P99 must stay under 200ms. Cache miss path must be profiled before any new evaluation step is added.
5. **Audit log is always write-only.** If you find yourself writing a query that filters, updates, or deletes audit logs: stop and revisit the requirement.
