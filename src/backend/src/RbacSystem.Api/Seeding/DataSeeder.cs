using AuditLogging.Domain.Entities;
using AuditLogging.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure;
using Delegation.Domain.Entities;
using Delegation.Infrastructure.Persistence;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PolicyEngine.Domain.Entities;
using PolicyEngine.Infrastructure.Persistence;
using RbacCore.Domain.Entities;
using RbacCore.Infrastructure.Persistence;
using TenantManagement.Domain.Entities;
using TenantManagement.Infrastructure.Persistence;

namespace RbacSystem.Api.Infrastructure;

// ─────────────────────────────────────────────────────────────────────────────
// WIRING (Program.cs)
//
//   builder.Services.AddTransient<DataSeeder>();
//
//   if (app.Environment.IsDevelopment())
//   {
//       await using var scope = app.Services.CreateAsyncScope();
//       await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
//   }
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Idempotent seed data generator for development and integration testing.
/// Covers every Phase 2 table:
///   tenant     → Tenants
///   identity   → users, user_credentials
///   rbac       → Roles, Permissions, RolePermissions, UserRoleAssignments,
///                Scopes, ScopeHierarchy
///   policy     → Policies  (includes one soft-deleted policy)
///   delegation → Delegations  (active, revoked, expired)
///   audit      → AuditLogs  (access decisions + admin actions)
///
/// All checks use business keys so re-running is always safe.
///
/// Seeded accounts  (password: Seed@Test123!)
///   admin@acme.test      → tenant-admin  (all permissions, tenant-wide)
///   editor@acme.test     → content-editor (tenant-wide)
///                          + viewer (Engineering scope only)
///   viewer@acme.test     → viewer (tenant-wide)
///   delegator@acme.test  → content-editor (Project Alpha scope only)
///   delegatee@acme.test  → no direct roles; receives delegation from delegator
/// </summary>
public sealed class DataSeeder
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<DataSeeder> _logger;

    public const string SeedPassword = "Seed@Test123!";
    public const string SeedTenantSlug = "acme-test";

    public DataSeeder(IServiceProvider provider, ILogger<DataSeeder> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    // ── Entry point ───────────────────────────────────────────────────────────

    public async Task SeedAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[DataSeeder] Starting...");

        // All DbContexts share one SuperAdmin context so every global query
        // filter (tenant isolation, soft-delete, expiry) is bypassed.
        var sys = new SeedSuperAdminContext();

        await using var tenantDb = new TenantDbContext(Opts<TenantDbContext>(), sys);
        await using var identityDb = new IdentityDbContext(Opts<IdentityDbContext>(), sys);
        await using var rbacDb = new RbacDbContext(Opts<RbacDbContext>(), sys);
        await using var policyDb = new PolicyDbContext(Opts<PolicyDbContext>(), sys);
        await using var delegationDb = new DelegationDbContext(Opts<DelegationDbContext>(), sys);
        await using var auditDb = new AuditDbContext(Opts<AuditDbContext>(), sys);

        // AFTER
        var hasher = _provider.GetRequiredService<IPasswordHasher>();

        var tenantId = await SeedTenantAsync(tenantDb, ct);
        var users = await SeedUsersAsync(identityDb, hasher, tenantId, ct);

        var perms = await SeedPermissionsAsync(rbacDb, tenantId, users["admin"], ct);
        rbacDb.ChangeTracker.Clear(); // detach Permission inserts before Role load

        var roles = await SeedRolesAsync(rbacDb, tenantId, users["admin"], ct);
        rbacDb.ChangeTracker.Clear(); // detach Role inserts before Include() re-load in AssignPermissions

        await AssignPermissionsToRolesAsync(rbacDb, tenantId, roles, perms, users["admin"], ct);
        rbacDb.ChangeTracker.Clear(); // detach RolePermission inserts before Scope load

        var scopes = await SeedScopesAsync(rbacDb, tenantId, users["admin"], ct);
        rbacDb.ChangeTracker.Clear(); // detach Scope/Hierarchy inserts before UserRoleAssignment load

        await AssignRolesToUsersAsync(rbacDb, tenantId, users, roles, scopes, ct);
        await SeedPoliciesAsync(policyDb, tenantId, users["admin"], ct);
        await SeedDelegationsAsync(delegationDb, tenantId, users, scopes, ct);
        await SeedAuditLogsAsync(auditDb, tenantId, users, ct);

        LogManifest(tenantId, users, roles, scopes);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private DbContextOptions<T> Opts<T>() where T : DbContext
        => _provider.GetRequiredService<DbContextOptions<T>>();

    // ── Tenant ────────────────────────────────────────────────────────────────

    private async Task<Guid> SeedTenantAsync(TenantDbContext db, CancellationToken ct)
    {
        var existing = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug.Value == SeedTenantSlug, ct);

        if (existing is not null)
        {
            _logger.LogInformation("[DataSeeder] Tenant exists ({Id})", existing.Id);
            return existing.Id;
        }

        var tenant = Tenant.Create(
            "Acme Corporation (Test)",
            TenantManagement.Domain.ValueObjects.TenantSlug.Create(SeedTenantSlug),
            createdByUserId: Guid.Empty);   // system sentinel — no user yet

        await db.Tenants.AddAsync(tenant, ct);
        await db.SaveChangesAsync(ct);
        tenant.ClearDomainEvents();

        _logger.LogInformation("[DataSeeder] Created tenant {Id}", tenant.Id);
        return tenant.Id;
    }

    // ── Users + Credentials ───────────────────────────────────────────────────

    private async Task<Dictionary<string, Guid>> SeedUsersAsync(
        IdentityDbContext db,
        IPasswordHasher hasher,
        Guid tenantId,
        CancellationToken ct)
    {
        var seeds = new (string Key, string Email, string DisplayName)[]
        {
            ("admin",     "admin@acme.test",     "Tenant Admin"),
            ("editor",    "editor@acme.test",    "Alice Editor"),
            ("viewer",    "viewer@acme.test",    "Bob Viewer"),
            ("delegator", "delegator@acme.test", "Carol Delegator"),
            ("delegatee", "delegatee@acme.test", "Dave Delegatee"),
        };

        var result = new Dictionary<string, Guid>();

        foreach (var (key, email, displayName) in seeds)
        {
            var existing = await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email.Value == email && u.TenantId == tenantId, ct);

            if (existing is not null)
            {
                result[key] = existing.Id;
                _logger.LogDebug("[DataSeeder] User exists: {Email}", email);
                continue;
            }

            var user = User.Create(
                tenantId,
                Identity.Domain.ValueObjects.Email.Create(email),
                Identity.Domain.ValueObjects.DisplayName.Create(displayName),
                createdByUserId: Guid.Empty);

            var (hash, salt) = hasher.HashPassword(SeedPassword);
            var credential = UserCredential.Create(user.Id, tenantId, hash, salt);

            await db.Users.AddAsync(user, ct);
            await db.UserCredentials.AddAsync(credential, ct);
            await db.SaveChangesAsync(ct);
            user.ClearDomainEvents();

            result[key] = user.Id;
            _logger.LogInformation("[DataSeeder] Created user: {Email} ({Id})", email, user.Id);
        }

        return result;
    }

    // ── Permissions ───────────────────────────────────────────────────────────

    private static readonly (string Code, string Resource, string Action, string Desc)[]
        PermissionDefs =
    {
        ("users:read",           "users",         "read",   "Read user profiles"),
        ("users:create",         "users",         "create", "Create new users"),
        ("users:update",         "users",         "update", "Update user profiles"),
        ("users:delete",         "users",         "delete", "Delete users"),
        ("roles:read",           "roles",         "read",   "Read roles"),
        ("roles:create",         "roles",         "create", "Create roles"),
        ("roles:update",         "roles",         "update", "Update roles"),
        ("roles:delete",         "roles",         "delete", "Delete roles"),
        ("roles:assign",         "roles",         "assign", "Assign roles to users"),
        ("roles:revoke",         "roles",         "revoke", "Revoke roles from users"),
        ("permissions:read",     "permissions",   "read",   "Read permissions"),
        ("permissions:grant",    "permissions",   "grant",  "Grant permissions to roles"),
        ("permissions:revoke",   "permissions",   "revoke", "Revoke permissions from roles"),
        ("policies:read",        "policies",      "read",   "Read policies"),
        ("policies:create",      "policies",      "create", "Create policies"),
        ("policies:update",      "policies",      "update", "Update policies"),
        ("policies:delete",      "policies",      "delete", "Delete policies"),
        ("delegations:read",     "delegations",   "read",   "Read delegations"),
        ("delegations:create",   "delegations",   "create", "Create delegations"),
        ("delegations:revoke",   "delegations",   "revoke", "Revoke delegations"),
        ("audit-logs:read",      "audit-logs",    "read",   "Read audit logs"),
        ("audit-logs:export",    "audit-logs",    "export", "Export audit logs to CSV"),
        ("tenant-config:read",   "tenant-config", "read",   "Read tenant configuration"),
        ("tenant-config:update", "tenant-config", "update", "Update tenant configuration"),
    };

    private async Task<Dictionary<string, Guid>> SeedPermissionsAsync(
        RbacDbContext db, Guid tenantId, Guid adminId, CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>();

        foreach (var (code, resource, action, desc) in PermissionDefs)
        {
            var existing = await db.Permissions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Code.Value == code && p.TenantId == tenantId, ct);

            if (existing is not null) { result[code] = existing.Id; continue; }

            var perm = Permission.Create(tenantId, code, resource, action, desc, adminId);
            await db.Permissions.AddAsync(perm, ct);
            result[code] = perm.Id;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[DataSeeder] Seeded {Count} permissions", result.Count);
        return result;
    }

    // ── Roles ─────────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, Guid>> SeedRolesAsync(
        RbacDbContext db, Guid tenantId, Guid adminId, CancellationToken ct)
    {
        var seeds = new (string Name, string Desc, bool IsSystem)[]
        {
            ("tenant-admin",   "Full administrative access within this tenant.", true),
            ("content-editor", "Manage policies; read most resources.",          false),
            ("viewer",         "Read-only access to all resources.",             false),
        };

        var result = new Dictionary<string, Guid>();

        foreach (var (name, desc, isSystem) in seeds)
        {
            var existing = await db.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Name == name && r.TenantId == tenantId, ct);

            if (existing is not null) { result[name] = existing.Id; continue; }

            var role = Role.Create(tenantId, name, desc, adminId, isSystem);
            role.ClearDomainEvents();
            await db.Roles.AddAsync(role, ct);
            result[name] = role.Id;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[DataSeeder] Seeded {Count} roles", result.Count);
        return result;
    }

    // ── Role → Permission assignments ─────────────────────────────────────────

    private async Task AssignPermissionsToRolesAsync(
        RbacDbContext db,
        Guid tenantId,
        Dictionary<string, Guid> roles,
        Dictionary<string, Guid> perms,
        Guid adminId,
        CancellationToken ct)
    {
        var matrix = new Dictionary<string, string[]>
        {
            ["tenant-admin"] = perms.Keys.ToArray(),

            ["content-editor"] =
            [
                "users:read",
            "roles:read", "roles:assign", "roles:revoke",
            "permissions:read",
            "policies:read", "policies:create", "policies:update",
            "delegations:read", "delegations:create", "delegations:revoke",
            "audit-logs:read",
            "tenant-config:read",
        ],

            ["viewer"] =
            [
                "users:read", "roles:read", "permissions:read",
            "policies:read", "delegations:read",
            "audit-logs:read", "tenant-config:read",
        ],
        };

        foreach (var (roleName, codes) in matrix)
        {
            if (!roles.TryGetValue(roleName, out var roleId)) continue;

            // Load only the existing RolePermission rows — never load the Role aggregate.
            // This avoids EF tracking the Role entity and attempting a concurrency UPDATE.
            var existing = (await db.RolePermissions
    .Where(rp => rp.RoleId == roleId)
    .Select(rp => rp.PermissionId)
    .ToListAsync(ct)).ToHashSet();

            foreach (var code in codes)
            {
                if (!perms.TryGetValue(code, out var permId)) continue;
                if (existing.Contains(permId)) continue;

                // Insert RolePermission directly — bypass Role aggregate entirely.
                var rp = new
                {
                    Id = Guid.NewGuid(),
                    RoleId = roleId,
                    PermissionId = permId,
                    TenantId = tenantId,
                    GrantedByUserId = adminId,
                    GrantedAt = DateTimeOffset.UtcNow,
                };

                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                INSERT INTO rbac."RolePermissions"
                    ("Id", "RoleId", "PermissionId", "TenantId", "GrantedByUserId", "GrantedAt")
                VALUES
                    ({rp.Id}, {rp.RoleId}, {rp.PermissionId}, {rp.TenantId}, {rp.GrantedByUserId}, {rp.GrantedAt})
                ON CONFLICT DO NOTHING
                """, ct);
            }
        }

        _logger.LogInformation("[DataSeeder] Role-permission assignments done");
    }

    // ── Scopes + Closure Table ────────────────────────────────────────────────
    //
    // Acme Corp (org)
    // ├── Engineering (dept)
    // │   ├── Project Alpha (project)
    // │   └── Project Beta  (project)
    // └── Marketing (dept)

    private async Task<Dictionary<string, Guid>> SeedScopesAsync(
        RbacDbContext db, Guid tenantId, Guid adminId, CancellationToken ct)
    {
        // Parents must appear before children.
        var seeds = new (string Key, string Name, ScopeType Type, string? Parent)[]
        {
            ("org",         "Acme Corp",     ScopeType.Organization, null),
            ("engineering", "Engineering",   ScopeType.Department,   "org"),
            ("marketing",   "Marketing",     ScopeType.Department,   "org"),
            ("alpha",       "Project Alpha", ScopeType.Project,      "engineering"),
            ("beta",        "Project Beta",  ScopeType.Project,      "engineering"),
        };

        var result = new Dictionary<string, Guid>();

        foreach (var (key, name, type, parentKey) in seeds)
        {
            var existing = await db.Scopes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Name == name && s.TenantId == tenantId, ct);

            if (existing is not null) { result[key] = existing.Id; continue; }

            Guid? parentId = parentKey is not null ? result[parentKey] : null;

            var scope = Scope.Create(tenantId, name, type, parentId, description: null, adminId);
            await db.Scopes.AddAsync(scope, ct);
            await db.SaveChangesAsync(ct);

            await InsertClosureRowsAsync(db, tenantId, scope.Id, parentId, ct);
            result[key] = scope.Id;
        }

        _logger.LogInformation("[DataSeeder] Seeded {Count} scopes", result.Count);
        return result;
    }

    /// <summary>
    /// Standard closure-table insertion:
    ///   (self → self, depth 0)
    ///   (each ancestor of parent → self, ancestor.depth + 1)
    /// </summary>
    private static async Task InsertClosureRowsAsync(
        RbacDbContext db, Guid tenantId,
        Guid newScopeId, Guid? parentId, CancellationToken ct)
    {
        var rows = new List<ScopeHierarchy>
        {
            ScopeHierarchy.Create(tenantId, newScopeId, newScopeId, 0)
        };

        if (parentId.HasValue)
        {
            var parentAncestors = await db.ScopeHierarchies
                .IgnoreQueryFilters()
                .Where(sh => sh.DescendantId == parentId.Value && sh.TenantId == tenantId)
                .ToListAsync(ct);

            rows.AddRange(parentAncestors.Select(a =>
                ScopeHierarchy.Create(tenantId, a.AncestorId, newScopeId, a.Depth + 1)));
        }

        await db.ScopeHierarchies.AddRangeAsync(rows, ct);
        await db.SaveChangesAsync(ct);
    }

    // ── User → Role assignments ───────────────────────────────────────────────

    private async Task AssignRolesToUsersAsync(
        RbacDbContext db,
        Guid tenantId,
        Dictionary<string, Guid> users,
        Dictionary<string, Guid> roles,
        Dictionary<string, Guid> scopes,
        CancellationToken ct)
    {
        // (userKey, roleKey, scopeKey — null = tenant-wide, expiresAt)
        var assignments = new (string U, string R, string? S, DateTimeOffset? Exp)[]
        {
            ("admin",     "tenant-admin",   null,          null),
            ("editor",    "content-editor", null,          null),
            ("editor",    "viewer",         "engineering", null),   // scope-restricted
            ("viewer",    "viewer",         null,          null),
            ("delegator", "content-editor", "alpha",       null),
            // Time-limited assignment — tests ExpiresAt filtering
            ("admin",     "viewer",         null,          DateTimeOffset.UtcNow.AddHours(1)),
        };

        foreach (var (u, r, s, exp) in assignments)
        {
            Guid? scopeId = s is not null ? scopes[s] : null;

            var exists = await db.UserRoleAssignments
                .IgnoreQueryFilters()
                .AnyAsync(a =>
                    a.UserId == users[u] && a.RoleId == roles[r] &&
                    a.TenantId == tenantId && a.ScopeId == scopeId, ct);

            if (exists) continue;

            var a = UserRoleAssignment.Create(
                tenantId, users[u], roles[r], scopeId, exp,
                assignedByUserId: users["admin"]);
            a.ClearDomainEvents();
            await db.UserRoleAssignments.AddAsync(a, ct);
        }

        // One pre-deactivated assignment — verifies IsActive = false is excluded
        // by the global query filter while still being stored in the table.
        var deactivatedExists = await db.UserRoleAssignments
            .IgnoreQueryFilters()
            .AnyAsync(a =>
                a.UserId == users["viewer"] && a.RoleId == roles["content-editor"] &&
                a.TenantId == tenantId && !a.IsActive, ct);

        if (!deactivatedExists)
        {
            var dead = UserRoleAssignment.Create(
                tenantId, users["viewer"], roles["content-editor"],
                scopeId: null, expiresAt: null, assignedByUserId: users["admin"]);
            dead.Deactivate("TestDeactivation", users["admin"]);
            dead.ClearDomainEvents();
            await db.UserRoleAssignments.AddAsync(dead, ct);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[DataSeeder] User-role assignments done");
    }

    // ── Policies ──────────────────────────────────────────────────────────────

    private async Task SeedPoliciesAsync(
        PolicyDbContext db, Guid tenantId, Guid adminId, CancellationToken ct)
    {
        var seeds = new (
            string Name, string Desc, PolicyEffect Effect,
            string Json, Guid? Resource, string? Action)[]
        {
            (
                "Allow During Business Hours",
                "Allows access only when the request arrives between 08:00-18:00 UTC.",
                PolicyEffect.Allow,
                """{"operator":"And","conditions":[{"attribute":"env.time_utc","op":"Between","value":["08:00","18:00"]}]}""",
                null, null
            ),
            (
                "Deny Delete Outside Business Hours",
                "Blocks users:delete when the wall-clock is outside 08:00-18:00 UTC.",
                PolicyEffect.Deny,
                """{"operator":"Not","conditions":[{"attribute":"env.time_utc","op":"Between","value":["08:00","18:00"]}]}""",
                null, "users:delete"
            ),
            (
                "Engineering Department Only",
                "Allows access only for users whose department attribute equals Engineering.",
                PolicyEffect.Allow,
                """{"operator":"And","conditions":[{"attribute":"user.department","op":"Eq","value":"Engineering"}]}""",
                null, null
            ),
            (
                "Internal Network Only",
                "Allows access only from the internal 10.0.x.x subnet.",
                PolicyEffect.Allow,
                """{"operator":"And","conditions":[{"attribute":"env.ip","op":"StartsWith","value":"10.0."}]}""",
                null, null
            ),
            (
                "Deny Tenant Config Updates for Non-Admins",
                "Denies tenant-config:update unless user carries platform-admin attribute.",
                PolicyEffect.Deny,
                """{"operator":"And","conditions":[{"attribute":"user.department","op":"Neq","value":"platform-admin"}]}""",
                null, "tenant-config:update"
            ),
            (
                "Multi-Condition ABAC: Eng + Hours + Network",
                "Composite: Engineering dept AND business hours AND internal network.",
                PolicyEffect.Allow,
                """{"operator":"And","conditions":[{"attribute":"user.department","op":"Eq","value":"Engineering"},{"attribute":"env.time_utc","op":"Between","value":["08:00","18:00"]},{"attribute":"env.ip","op":"StartsWith","value":"10.0."}]}""",
                null, null
            ),
        };

        foreach (var (name, desc, effect, json, resource, action) in seeds)
        {
            var exists = await db.Policies
                .IgnoreQueryFilters()
                .AnyAsync(p => p.Name == name && p.TenantId == tenantId, ct);

            if (exists) continue;

            var policy = Policy.Create(
                tenantId, name, desc, effect, json, resource, action, adminId);
            policy.ClearDomainEvents();
            await db.Policies.AddAsync(policy, ct);
        }

        // Soft-deleted policy — verifies IsDeleted global filter behaviour
        var deletedExists = await db.Policies
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Name == "DELETED: Old IP Restriction" && p.TenantId == tenantId, ct);

        if (!deletedExists)
        {
            var dead = Policy.Create(
                tenantId, "DELETED: Old IP Restriction",
                "Retired — kept only to exercise soft-delete filtering in tests.",
                PolicyEffect.Deny,
                """{"operator":"And","conditions":[{"attribute":"env.ip","op":"StartsWith","value":"192.168."}]}""",
                null, null, adminId);
            dead.SoftDelete(adminId);
            dead.ClearDomainEvents();
            await db.Policies.AddAsync(dead, ct);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[DataSeeder] Seeded policies");
    }

    // ── Delegations ───────────────────────────────────────────────────────────

    private async Task SeedDelegationsAsync(
        DelegationDbContext db,
        Guid tenantId,
        Dictionary<string, Guid> users,
        Dictionary<string, Guid> scopes,
        CancellationToken ct)
    {
        var anyExists = await db.Delegations
            .IgnoreQueryFilters()
            .AnyAsync(d =>
                d.DelegatorId == users["delegator"] &&
                d.DelegateeId == users["delegatee"] &&
                d.TenantId == tenantId, ct);

        if (anyExists)
        {
            _logger.LogInformation("[DataSeeder] Delegations already seeded");
            return;
        }

        // 1. ACTIVE — delegator grants users:read + roles:read on Project Alpha
        var active = DelegationGrant.Create(
            tenantId, users["delegator"], users["delegatee"],
            permissionCodes: ["users:read", "roles:read"],
            scopeId: scopes["alpha"],
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            chainDepth: 1,
            createdByUserId: users["delegator"]);
        active.ClearDomainEvents();
        await db.Delegations.AddAsync(active, ct);

        // 2. REVOKED — created then immediately revoked by admin
        var revoked = DelegationGrant.Create(
            tenantId, users["delegator"], users["delegatee"],
            permissionCodes: ["delegations:read"],
            scopeId: scopes["org"],
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            chainDepth: 1,
            createdByUserId: users["delegator"]);
        revoked.Revoke(revokedByUserId: users["admin"]);
        revoked.ClearDomainEvents();
        await db.Delegations.AddAsync(revoked, ct);

        // 3. EXPIRED — domain invariant blocks past expiresAt in Create(),
        //    so we create with a brief future window then backdate via raw SQL.
        var expired = DelegationGrant.Create(
            tenantId, users["delegator"], users["delegatee"],
            permissionCodes: ["policies:read"],
            scopeId: scopes["engineering"],
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(10),
            chainDepth: 1,
            createdByUserId: users["delegator"]);
        expired.ClearDomainEvents();
        await db.Delegations.AddAsync(expired, ct);
        await db.SaveChangesAsync(ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE delegation."Delegations" SET "ExpiresAt" = {DateTimeOffset.UtcNow.AddDays(-1)} WHERE "Id" = {expired.Id}""",
            ct);

        _logger.LogInformation(
            "[DataSeeder] Delegations: active={A} revoked={R} expired={E}",
            active.Id, revoked.Id, expired.Id);
    }

    // ── Audit Logs ────────────────────────────────────────────────────────────

    private async Task SeedAuditLogsAsync(
        AuditDbContext db, Guid tenantId,
        Dictionary<string, Guid> users, CancellationToken ct)
    {
        var count = await db.AuditLogs
            .IgnoreQueryFilters()
            .CountAsync(a => a.TenantId == tenantId, ct);

        if (count > 0)
        {
            _logger.LogInformation("[DataSeeder] Audit logs already seeded ({Count})", count);
            return;
        }

        var correlation = Guid.NewGuid();
        var resA = Guid.NewGuid();
        var resB = Guid.NewGuid();
        var sc = Guid.NewGuid();

        var logs = new List<AuditLog>
        {
            // GRANTED — cache miss
            AuditLog.CreateAccessDecision(
                tenantId, correlation, users["editor"], "users:read",
                resA, sc, isGranted: true, denialReason: null,
                cacheHit: false, evaluationLatencyMs: 14, policyId: null, delegationChain: null),

            // GRANTED — cache hit
            AuditLog.CreateAccessDecision(
                tenantId, Guid.NewGuid(), users["viewer"], "roles:read",
                resA, sc, isGranted: true, denialReason: null,
                cacheHit: true, evaluationLatencyMs: 1, policyId: null, delegationChain: null),

            // DENIED — NoPermissionFound
            AuditLog.CreateAccessDecision(
                tenantId, Guid.NewGuid(), users["viewer"], "users:delete",
                resA, sc, isGranted: false, denialReason: "NoPermissionFound",
                cacheHit: false, evaluationLatencyMs: 18, policyId: null, delegationChain: null),

            // DENIED — AbacConditionFailed
            AuditLog.CreateAccessDecision(
                tenantId, Guid.NewGuid(), users["editor"], "users:delete",
                resB, sc, isGranted: false, denialReason: "AbacConditionFailed",
                cacheHit: false, evaluationLatencyMs: 45,
                policyId: "Deny Delete Outside Business Hours", delegationChain: null),

            // DENIED — ExplicitGlobalDeny
            AuditLog.CreateAccessDecision(
                tenantId, Guid.NewGuid(), users["delegator"], "tenant-config:update",
                resA, sc, isGranted: false, denialReason: "ExplicitGlobalDeny",
                cacheHit: false, evaluationLatencyMs: 7,
                policyId: "Deny Tenant Config Updates for Non-Admins", delegationChain: null),

            // GRANTED — via delegation chain
            AuditLog.CreateAccessDecision(
                tenantId, Guid.NewGuid(), users["delegatee"], "users:read",
                resA, sc, isGranted: true, denialReason: null,
                cacheHit: false, evaluationLatencyMs: 22, policyId: null,
                delegationChain: $"{users["delegator"]}\u2192{users["delegatee"]}"),

            // DENIED — DelegationExpired
            AuditLog.CreateAccessDecision(
                tenantId, Guid.NewGuid(), users["delegatee"], "policies:read",
                resB, sc, isGranted: false, denialReason: "DelegationExpired",
                cacheHit: false, evaluationLatencyMs: 9, policyId: null,
                delegationChain: $"{users["delegator"]}\u2192{users["delegatee"]}"),

            // DENIED — DelegationRevoked
            AuditLog.CreateAccessDecision(
                tenantId, Guid.NewGuid(), users["delegatee"], "delegations:read",
                resB, sc, isGranted: false, denialReason: "DelegationRevoked",
                cacheHit: false, evaluationLatencyMs: 6, policyId: null,
                delegationChain: $"{users["delegator"]}\u2192{users["delegatee"]}"),

            // AdminAction — role created
            AuditLog.CreateAdminAction(
                tenantId, correlation, users["admin"],
                "roles:create", "Role", Guid.NewGuid(),
                oldValue: null, newValue: """{"name":"content-editor"}"""),

            // AdminAction — user created
            AuditLog.CreateAdminAction(
                tenantId, Guid.NewGuid(), users["admin"],
                "users:create", "User", users["editor"],
                oldValue: null, newValue: """{"email":"editor@acme.test"}"""),

            // AdminAction — role assigned
            AuditLog.CreateAdminAction(
                tenantId, Guid.NewGuid(), users["admin"],
                "roles:assign", "UserRoleAssignment", Guid.NewGuid(),
                oldValue: null,
                newValue: $$$"""{"userId":"{{{users["editor"]}}}","role":"content-editor"}"""),

            // AdminAction — policy created
            AuditLog.CreateAdminAction(
                tenantId, Guid.NewGuid(), users["admin"],
                "policies:create", "Policy", Guid.NewGuid(),
                oldValue: null,
                newValue: """{"name":"Allow During Business Hours","effect":"Allow"}"""),

            // AdminAction — delegation revoked
            AuditLog.CreateAdminAction(
                tenantId, Guid.NewGuid(), users["admin"],
                "delegations:revoke", "Delegation", Guid.NewGuid(),
                oldValue: null,
                newValue: $$$"""{"delegateeId":"{{{users["delegatee"]}}}"}"""),
        };

        foreach (var log in logs)
            await db.AuditLogs.AddAsync(log, ct);

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[DataSeeder] Seeded {Count} audit log entries", logs.Count);
    }

    // ── Manifest ──────────────────────────────────────────────────────────────

    private void LogManifest(
        Guid tenantId,
        Dictionary<string, Guid> users,
        Dictionary<string, Guid> roles,
        Dictionary<string, Guid> scopes)
    {
        _logger.LogInformation("[DataSeeder] ┌── SEED MANIFEST ──────────────────────────────");
        _logger.LogInformation("[DataSeeder] │  Tenant slug : {Slug}", SeedTenantSlug);
        _logger.LogInformation("[DataSeeder] │  Tenant ID   : {Id}", tenantId);
        _logger.LogInformation("[DataSeeder] │  Password    : {Pwd}", SeedPassword);
        _logger.LogInformation("[DataSeeder] ├── Users ───────────────────────────────────────");
        foreach (var (k, id) in users)
            _logger.LogInformation("[DataSeeder] │  {Key,-12} → {Id}", k, id);
        _logger.LogInformation("[DataSeeder] ├── Roles ───────────────────────────────────────");
        foreach (var (k, id) in roles)
            _logger.LogInformation("[DataSeeder] │  {Key,-16} → {Id}", k, id);
        _logger.LogInformation("[DataSeeder] ├── Scopes ──────────────────────────────────────");
        foreach (var (k, id) in scopes)
            _logger.LogInformation("[DataSeeder] │  {Key,-12} → {Id}", k, id);
        _logger.LogInformation("[DataSeeder] └─────────────────────────────────────────────────");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// file-scoped: not visible outside DataSeeder.cs
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Bypasses every EF Core global query filter during seeding by advertising
/// IsSuperAdmin = true. Never use this outside of DataSeeder.
/// </summary>
file sealed class SeedSuperAdminContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;   // irrelevant when IsSuperAdmin = true
    public bool IsSuperAdmin => true;
    public Guid UserId => Guid.Empty;
}
