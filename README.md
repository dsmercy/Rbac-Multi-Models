# Enterprise RBAC System

A scalable, enterprise-ready **Role-Based Access Control** platform with multi-tenancy, ABAC policies, scoped hierarchy, time-bound delegation, and full auditability — built on ASP.NET Core 8 (Clean Architecture) and React + TypeScript.

---

## Core Capabilities

- **Multi-tenancy** — single instance serves multiple isolated organisations via row-level tenant isolation
- **RBAC** — permissions grouped into roles, assigned to users per scope
- **Scoped hierarchy** — organisation → department → project, with controlled downward inheritance
- **ABAC** — access decisions based on user, resource, and environment attributes
- **Policy engine** — dynamic JSON condition tree evaluation (AWS IAM-style), stored and evaluated at runtime
- **Delegation** — time-bound, audited, chain-limited permission delegation between users
- **Auditability** — immutable, append-only log of every access decision and administrative action

---

## Tech Stack

| Concern | Technology |
|---|---|
| Framework | ASP.NET Core 8 Web API |
| Architecture | Modular Monolith — Clean Architecture |
| CQRS | MediatR |
| Database | PostgreSQL 16 (primary), Azure SQL (supported) |
| ORM | Entity Framework Core 8 + Dapper (read-heavy paths) |
| Caching | Redis 7 |
| Authentication | JWT Bearer + OAuth2 / OpenID Connect |
| Frontend | React + TypeScript (Admin Panel) |
| Observability | Serilog → structured logs, OpenTelemetry tracing, Prometheus metrics |
| Messaging (future) | Kafka / Azure Service Bus |

---

## Project Structure

```
/src
  /Modules
    /Identity                  # User identity, credentials, authentication
    /TenantManagement          # Tenant lifecycle, config, bootstrapping
    /RbacCore                  # Roles, permissions, assignments
    /PermissionEngine          # CanUserAccess evaluator
    /PolicyEngine              # JSON condition tree storage & evaluation
    /Delegation                # Time-bound delegation lifecycle
    /Audit                     # Append-only event log
  /SharedKernel
    SharedKernel.Domain        # Base entity, aggregate root, domain events
    SharedKernel.Application   # CQRS interfaces, ICurrentTenantContext
    SharedKernel.Infrastructure # EF base context, Redis client, outbox pattern
  /ApiGateway                  # Single entry-point API project
```

Each module follows `Domain → Application → Infrastructure → API` layers and is independently extractable into a microservice.

---

## Architecture Decisions

### Multi-Tenancy

Row-level isolation — every tenant-scoped table carries a `TenantId` column. Global EF Core query filters inject `TenantId` automatically from the ambient request context. No query may manually add `WHERE TenantId = ?`.

**Super-admin** operates outside tenant isolation for platform-level operations only. All super-admin usage is fully audited.

### Tenant Bootstrapping

On first tenant creation, the system automatically seeds: a default admin user, a `tenant-admin` role with full intra-tenant permissions, and a set of default permission templates — solving the chicken-and-egg problem at creation time.

### Module Communication

Modules communicate exclusively via:
- Direct calls to **published service interfaces**
- **Domain events** via an in-process MediatR event bus (swappable to Kafka / Service Bus without changing publishers)

No module may reference another module's `Infrastructure` or `Domain` project directly. Cross-module data reads use projections — no cross-module DB joins.

### Soft Delete

Roles, permissions, and assignments are **never physically deleted**. A `DeletedAt` timestamp marks deletion; EF Core global query filters exclude soft-deleted records automatically. When a role is soft-deleted, all active `RoleAssignments` referencing it are automatically deactivated with `DeactivatedReason = "RoleDeleted"`.

---

## Permission Evaluation Engine

Core method:
```
Task<AccessResult> CanUserAccess(UserId, Action, ResourceId, ScopeId, EvaluationContext)
```

### Evaluation Pipeline (strict precedence)

| Step | Rule | Short-circuit |
|---|---|---|
| 1 | Explicit global DENY | Yes — return Denied immediately |
| 2 | Resource-level override (exact ResourceId) | On DENY — yes; on ALLOW — continue to step 3 |
| 3 | Delegation check (chain depth ≤ max, delegator still holds permission) | On invalid — deny |
| 4 | Scope inheritance resolution (walk hierarchy upward) | — |
| 5 | ABAC policy evaluation (JSON condition trees) | On any DENY — yes |
| 6 | Role-based permission check (deny-overrides-allow) | — |
| 7 | Default deny | Always — if no ALLOW reached |

### Conflict Resolution

- Explicit deny beats explicit allow at the same precedence level
- More specific scope (lower) overrides inherited permission from higher scope
- Among roles at the same scope: DENY wins
- Among policies: first DENY short-circuits

### AccessResult Shape

```json
{
  "result": "Granted | Denied",
  "deniedReason": "enum",
  "evaluatedPolicies": [],
  "effectiveRoles": [],
  "delegationChainUsed": null,
  "cacheHit": true,
  "evaluationLatencyMs": 4
}
```

---

## JWT Token Design

```json
{
  "sub": "uuid",
  "tid": "tenant-uuid",
  "email": "string",
  "roles": ["role-id-1"],
  "scp": ["org:uuid", "dept:uuid"],
  "del": "delegator-user-uuid | null",
  "del_chain": ["uuid1"],
  "is_super_admin": false,
  "tv": 3,
  "jti": "token-uuid",
  "iat": 0,
  "exp": 0
}
```

Roles and scopes are embedded in the token to avoid DB round-trips. A Redis `token-version:{userId}` key is checked on every permission evaluation — if the token's `tv` claim is stale, the request is rejected with `401` and forces re-authentication.

**Token version is incremented on:** `UserRoleAssigned`, `UserRoleRevoked`, `DelegationCreated`, `DelegationRevoked`.

| Token type | TTL |
|---|---|
| Access token | 15 minutes |
| Refresh token | 7 days (sliding) |

---

## Caching Architecture

### Cache Key Schema

```
perm:{tenantId}:{userId}:{action}:{resourceType}:{scopeId}  → AccessResult       TTL: 60s
roles:{tenantId}:{userId}                                    → string[] role IDs  TTL: 300s
policy:{tenantId}:{policyId}                                 → PolicyDocument     TTL: 600s
token-version:{userId}                                       → int version        TTL: sliding 7d
scope-tree:{tenantId}                                        → ScopeNode[]        TTL: 3600s
delegation:{tenantId}:{userId}                               → Delegation[]       TTL: 60s
```

### Cache Eviction Triggers

| Domain event | Keys invalidated |
|---|---|
| `UserRoleAssigned` / `UserRoleRevoked` | `perm:{tid}:{uid}:*`, `roles:{tid}:{uid}`, increment `token-version:{uid}` |
| `PolicyCreated/Updated/Deleted` | `perm:{tid}:*`, `policy:{tid}:{policyId}` |
| `RoleDeleted` | `perm:{tid}:*`, `roles:{tid}:*` for all affected users |
| `DelegationCreated/Revoked/Expired` | `perm:{tid}:{uid}:*`, `delegation:{tid}:{uid}`, increment `token-version:{uid}` |
| `ScopeUpdated` | `scope-tree:{tid}`, `perm:{tid}:*` |
| `TenantSuspended` | All keys under `*:{tid}:*` |

Stampede protection via Redis `SET NX` lock — first request past expiry acquires a 2s lock and recomputes; all others serve stale during recompute.

---

## Database Schema (Key Tables)

`Tenants`, `Users`, `Roles`, `Permissions`, `RolePermissions`, `UserRoleAssignments`, `Scopes`, `ScopeHierarchy` (closure table for arbitrary depth), `Policies`, `PolicyConditions`, `Delegations`, `AuditLogs`

All tenant-scoped tables carry `TenantId`. All mutable records carry `DeletedAt` / `IsDeleted`. `AuditLogs` is append-only with no update or delete operations permitted.

---

## API Reference

All endpoints use URL-path versioning (`/api/v1/`, `/api/v2/`). v1 is supported for 6 months after v2 ships. Deprecation is signalled via `Deprecation` and `Sunset` response headers.

### Tenant Management
| Method | Endpoint |
|---|---|
| POST | `/api/v1/tenants` |
| GET | `/api/v1/tenants/{id}` |
| PUT | `/api/v1/tenants/{id}` |
| DELETE | `/api/v1/tenants/{id}` (soft) |

### User Management
| Method | Endpoint |
|---|---|
| POST | `/api/v1/tenants/{tid}/users` |
| GET | `/api/v1/tenants/{tid}/users/{uid}` |
| PUT | `/api/v1/tenants/{tid}/users/{uid}` |

### Role & Permission Management
| Method | Endpoint | Description |
|---|---|---|
| POST / GET | `/api/v1/tenants/{tid}/roles` | Create / list roles |
| PUT / DELETE | `/api/v1/tenants/{tid}/roles/{rid}` | Update / soft-delete role |
| GET | `/api/v1/tenants/{tid}/roles/{rid}/permissions` | List role permissions |
| POST | `/api/v1/tenants/{tid}/roles/{rid}/permissions/{pid}` | Assign permission to role |
| DELETE | `/api/v1/tenants/{tid}/roles/{rid}/permissions/{pid}` | Revoke permission from role |

### User Role Assignments
| Method | Endpoint |
|---|---|
| POST | `/api/v1/tenants/{tid}/users/{uid}/roles` (assign role + scope) |
| DELETE | `/api/v1/tenants/{tid}/users/{uid}/roles/{rid}` |

### Policy Management
| Method | Endpoint |
|---|---|
| POST / GET | `/api/v1/tenants/{tid}/policies` |
| GET / PUT / DELETE | `/api/v1/tenants/{tid}/policies/{pid}` |

### Delegation
| Method | Endpoint |
|---|---|
| POST | `/api/v1/tenants/{tid}/delegations` |
| GET | `/api/v1/tenants/{tid}/delegations/{did}` |
| DELETE | `/api/v1/tenants/{tid}/delegations/{did}` (early revoke) |

### Permission Check
```
POST /api/v1/tenants/{tid}/permissions/check
Body: { userId, action, resourceId, scopeId }
Response: AccessResult (full evaluation detail)
```

### Audit Logs
```
GET /api/v1/tenants/{tid}/audit-logs
  ?from=&to=&userId=&action=&resourceId=&page=&pageSize=
Accept: text/csv   → CSV export
```

---

## Delegation Rules

- Chain depth defaults to **1** (delegatee cannot re-delegate). Configurable per tenant up to a hard platform limit of **3**.
- At delegation-creation time, every permission being delegated is validated against the delegator's current holdings.
- If the delegator later loses a permission, that delegation becomes **ineffective at evaluation time** — not at creation time.
- Early revocation immediately busts `delegation:{tid}:{uid}` cache, increments delegatee's `token-version`, and emits `DelegationRevoked`.
- Expired delegations are checked at evaluation time — no in-flight request can use an expired delegation.
- Every `AccessResult` produced via delegation records `ActingOnBehalfOf: { delegatorId, delegationId, chainDepth }` in the audit log.

---

## Failure Modes

| Failure | Behaviour |
|---|---|
| Redis unavailable | Fall through to DB. Log degraded mode. Configurable per deployment. |
| Eval timeout > 200ms | Return `Denied` (fail-closed). Log and alert if rate > 1%. |
| Partial policy failure | Skip errored rule, log warning. If > 50% of policies error, fail-closed. |
| Corrupt JWT | Return `401`. Log IP, user agent, token hash (never raw token). |
| DB unavailable during eval | Return `503`, not `403`. Infra failure must be distinguishable from permission denial. |

---

## Observability

### Structured Logs

Every `CanUserAccess` call emits:
`correlationId`, `tenantId`, `userId`, `action`, `resourceId`, `scopeId`, `result`, `deniedReason`, `cacheHit`, `evaluationLatencyMs`, `delegationChainUsed`, `policiesEvaluated[]`, `timestamp`

### Metrics (Prometheus / OpenTelemetry)

| Metric | Type |
|---|---|
| `rbac_eval_total{result, cache_hit, tenant_id}` | Counter |
| `rbac_eval_duration_ms{quantile}` | Histogram — P50/P95/P99 targets: <5ms cache hit, <50ms miss |
| `rbac_active_delegations{tenant_id}` | Gauge |
| `rbac_cache_evictions_total{key_type}` | Counter |
| `rbac_policy_eval_errors_total` | Counter |

### Alerting Rules

- Permission denial rate > 10% over 5 minutes (potential attack)
- `rbac_eval_duration_ms` P99 > 200ms
- `rbac_policy_eval_errors_total` > 0 for 2 consecutive minutes
- Redis connectivity lost
- Delegation chain depth > configured max detected

---

## Security

- Tenant isolation enforced at **three layers**: JWT claim validation (API), EF Core global query filter (infrastructure), explicit tenantId validation in every command/query handler (application)
- Privilege escalation prevented: a user cannot assign a role granting more permissions than they themselves hold — validated at assignment time via `CanUserAccess`
- All secrets via environment variables or Azure Key Vault — never in source or committed config
- OWASP API Top 10 mitigations: rate limiting per user and per tenant, FluentValidation on all inputs, no stack traces in error responses, full security headers (`CSP`, `HSTS`, `X-Frame-Options`)

---

## Admin Panel (React + TypeScript)

| View | Description |
|---|---|
| Role Editor | Create / edit roles, assign permissions via checkbox matrix, scope selector |
| Permission Matrix | Read-only grid: roles (columns) × permissions (rows), scope filter |
| User-Role Assignment | User search, role multi-select, scope picker, effective and expiry dates |
| Policy Builder | Visual JSON condition tree composer, preview rendered JSON, test against sample context |
| Delegation Manager | Create delegation, list active delegations, revoke with confirmation |
| Audit Log Viewer | Filterable table, full AccessResult detail on row expand, CSV export |
| Tenant Dashboard | Health summary — active users, roles, policies, recent denial chart |

The admin panel is governed by the same RBAC system it manages. An `<Authorized action="..." resource="...">` wrapper component disables or hides mutation controls based on the caller's own permissions. Real-time updates are pushed via SignalR — when a role or policy changes, an `rbac:invalidated` event is pushed to all connected admin clients for that tenant.

---

## Local Development

### Start the stack

```bash
docker compose up -d
```

Starts PostgreSQL 16, Redis 7, Jaeger (tracing), and Prometheus + Grafana (metrics).

### Run database migrations

EF Core migrations run idempotently on startup via a dedicated `DataSeeder` service. To run manually:

```bash
dotnet ef database update --project src/Modules/Identity/Identity.Infrastructure
```

### URLs

| Service | URL | Credentials |
|---|---|---|
| API (Swagger) | `https://localhost:5001/swagger` | — |
| Grafana | `http://localhost:3000` | admin / admin |
| Jaeger | `http://localhost:16686` | — |
| Prometheus | `http://localhost:9090` | — |

---

## Testing Strategy

- Every domain model has unit tests covering invariants (e.g. `Role` cannot have empty name, `Delegation` cannot have `ExpiresAt` in the past)
- `CanUserAccess` has a minimum of **20 unit test cases** covering: cache hit, each denial reason, valid delegation, expired delegation, scope inheritance, ABAC allow, ABAC deny, conflict resolution, super-admin bypass, cross-tenant rejection
- Stack: xUnit + FluentAssertions — only infrastructure dependencies (Redis, DB) are mocked

---

## Migration Strategy

- EF Core migrations exclusively — never modify an existing migration
- Zero-downtime schema changes via expand-contract pattern: add column nullable → backfill → add constraint → remove old column across separate deployments
- Seed data (default roles, permissions, system tenant) delivered via idempotent `DataSeeder` on startup
