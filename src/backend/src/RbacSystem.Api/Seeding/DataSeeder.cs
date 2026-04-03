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
/// Full-reset enterprise seed data generator for TechNova Solutions, CloudEdge Systems,
/// and NextGen AI Labs — three realistic IT company tenants with ~300 users each,
/// covering all RBAC, ABAC, delegation, and audit features.
///
/// FLOW: per-tenant: Tenants → Scopes → Users → Roles →
///       Permissions → RolePermissions → UserRoleAssignments → Delegations →
///       Policies → AuditLogs
///
/// Seeded password (all accounts): Seed@Test123!
/// </summary>
public sealed class DataSeeder
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<DataSeeder> _logger;

    public const string SeedPassword = "Seed@Test123!";

    public DataSeeder(IServiceProvider provider, ILogger<DataSeeder> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    // ── Tenant definitions ────────────────────────────────────────────────────

    private static readonly (string Name, string Slug, string Domain)[] TenantDefs =
    {
        ("TechNova Solutions",  "technova-solutions",  "technova.io"),
        ("CloudEdge Systems",   "cloudedge-systems",   "cloudedge.io"),
        ("NextGen AI Labs",     "nextgenai-labs",      "nextgenai.io"),
    };

    // ── Scope layout ─────────────────────────────────────────────────────────
    // (key, display name, type, parent key or null)

    private static readonly (string Key, string Name, ScopeType Type, string? Parent)[] ScopeDefs =
    {
        ("org",        "Organization",          ScopeType.Organization, null),
        ("eng",        "Engineering",           ScopeType.Department,   "org"),
        ("backend",    "Backend Team",          ScopeType.Project,      "eng"),
        ("frontend",   "Frontend Team",         ScopeType.Project,      "eng"),
        ("devops",     "DevOps Team",           ScopeType.Project,      "eng"),
        ("qa",         "QA Team",               ScopeType.Project,      "eng"),
        ("product",    "Product Management",    ScopeType.Department,   "org"),
        ("design",     "Design",                ScopeType.Department,   "org"),
        ("itsupport",  "IT Support",            ScopeType.Department,   "org"),
        ("security",   "Security & Compliance", ScopeType.Department,   "org"),
        ("dataana",    "Data & Analytics",      ScopeType.Department,   "org"),
        ("sales",      "Sales",                 ScopeType.Department,   "org"),
        ("marketing",  "Marketing",             ScopeType.Department,   "org"),
        ("finance",    "Finance",               ScopeType.Department,   "org"),
        ("hr",         "HR",                    ScopeType.Department,   "org"),
        ("csupport",   "Customer Support",      ScopeType.Department,   "org"),
    };

    // ── Department → user distribution ───────────────────────────────────────

    private static readonly (string ScopeKey, int Count, string PrimaryRole, string? ManagerRole)[] DeptDefs =
    {
        ("backend",   45, "BackendDeveloper",   "EngineeringManager"),
        ("frontend",  40, "FrontendDeveloper",  "EngineeringManager"),
        ("devops",    15, "DevOpsEngineer",      "EngineeringManager"),
        ("qa",        30, "QAEngineer",          "QALead"),
        ("product",   15, "ProductManager",      null),
        ("design",    12, "Designer",            null),
        ("itsupport", 12, "SupportAgent",        "SupportManager"),
        ("security",  10, "SecurityAdmin",       null),
        ("dataana",   12, "BackendDeveloper",    null),
        ("sales",     35, "SalesExecutive",      null),
        ("marketing", 22, "MarketingManager",    null),
        ("finance",    8, "FinanceManager",      null),
        ("hr",         8, "HRManager",           null),
        ("csupport",  36, "SupportAgent",        "SupportManager"),
    };  // total = 300

    // ── Name pools ────────────────────────────────────────────────────────────

    private static readonly string[] FirstNames =
    {
        "James","Emma","Liam","Olivia","Noah","Ava","William","Sophia","Benjamin","Isabella",
        "Lucas","Mia","Henry","Charlotte","Alexander","Amelia","Michael","Harper","Ethan","Evelyn",
        "Daniel","Abigail","Matthew","Emily","Aiden","Elizabeth","Jackson","Sofia","Sebastian","Avery",
        "David","Priya","Marcus","Mei","Carlos","Fatima","Ravi","Zara","Arjun","Leila",
        "Jordan","Morgan","Taylor","Alex","Casey","Riley","Jamie","Logan","Skylar","Peyton",
    };

    private static readonly string[] LastNames =
    {
        "Smith","Johnson","Williams","Brown","Jones","Garcia","Miller","Davis","Rodriguez","Martinez",
        "Hernandez","Lopez","Wilson","Anderson","Thomas","Taylor","Moore","Jackson","Martin","Lee",
        "Perez","Thompson","White","Harris","Sanchez","Clark","Lewis","Robinson","Walker","Young",
        "Allen","King","Wright","Scott","Torres","Nguyen","Hill","Flores","Green","Adams",
        "Nelson","Mitchell","Carter","Phillips","Evans","Turner","Parker","Collins","Edwards","Stewart",
        "Patel","Kumar","Singh","Shah","Ali","Chen","Zhang","Wang","Kim","Tanaka",
    };

    // ── Role definitions ──────────────────────────────────────────────────────

    private static readonly (string Name, string Desc, bool IsSystem)[] RoleDefs =
    {
        ("SuperAdmin",          "Platform-level super administrator.",                    true),
        ("TenantAdmin",         "Full administrative access within this tenant.",         true),
        ("SecurityAdmin",       "Cross-tenant security and compliance oversight.",        true),
        ("BackendDeveloper",    "Develop and maintain server-side services.",             false),
        ("FrontendDeveloper",   "Build and maintain UI components and applications.",     false),
        ("FullStackDeveloper",  "Full-stack engineer across frontend and backend.",       false),
        ("TechLead",            "Technical leadership with code review authority.",       false),
        ("EngineeringManager",  "Engineering team management and delivery oversight.",   false),
        ("QAEngineer",          "Quality assurance testing and automation.",              false),
        ("QALead",              "QA team lead with sign-off authority.",                 false),
        ("DevOpsEngineer",      "Infrastructure, CI/CD and platform reliability.",       false),
        ("SRE",                 "Site reliability engineering and on-call response.",    false),
        ("ProductManager",      "Product roadmap, requirements and prioritisation.",     false),
        ("Designer",            "UI/UX design and design system ownership.",             false),
        ("SalesExecutive",      "Client acquisition and account management.",            false),
        ("MarketingManager",    "Marketing campaigns and brand management.",             false),
        ("HRManager",           "People operations and talent management.",              false),
        ("FinanceManager",      "Financial reporting, budgeting and approvals.",         false),
        ("SupportAgent",        "Tier-1 customer support and issue resolution.",         false),
        ("SupportManager",      "Support team lead and escalation handler.",             false),
    };

    // ── Permission definitions ────────────────────────────────────────────────

    private static readonly (string Code, string Resource, string Action, string Desc)[] PermissionDefs =
    {
        // Users
        ("user:create",          "user",       "create",    "Create new users"),
        ("user:read",            "user",       "read",      "Read user profiles"),
        ("user:update",          "user",       "update",    "Update user profiles"),
        ("user:delete",          "user",       "delete",    "Delete / deactivate users"),
        // Roles
        ("role:create",          "role",       "create",    "Create roles"),
        ("role:read",            "role",       "read",      "Read roles"),
        ("role:update",          "role",       "update",    "Update roles and their permissions"),
        ("role:assign",          "role",       "assign",    "Assign roles to users"),
        ("role:delete",          "role",       "delete",    "Delete roles"),
        // Projects
        ("project:create",       "project",    "create",    "Create projects"),
        ("project:read",         "project",    "read",      "View project details"),
        ("project:update",       "project",    "update",    "Update project configuration"),
        ("project:deploy",       "project",    "deploy",    "Deploy project to production"),
        // Repositories
        ("repo:read",            "repo",       "read",      "Clone and pull repositories"),
        ("repo:write",           "repo",       "write",     "Push commits to repository"),
        ("repo:merge",           "repo",       "merge",     "Merge pull requests"),
        ("repo:admin",           "repo",       "admin",     "Manage repository settings"),
        // Infrastructure
        ("infra:deploy",         "infra",      "deploy",    "Deploy infrastructure changes"),
        ("infra:rollback",       "infra",      "rollback",  "Roll back a deployment"),
        ("infra:monitor",        "infra",      "monitor",   "View infrastructure metrics"),
        // Security
        ("security:audit",       "security",   "audit",     "Read security audit reports"),
        ("security:policy",      "security",   "policy",    "Update security policies"),
        // Finance
        ("invoice:read",         "invoice",    "read",      "View invoices and billing"),
        ("invoice:approve",      "invoice",    "approve",   "Approve and process invoices"),
        // HR / People
        ("employee:read",        "employee",   "read",      "View employee records"),
        ("employee:update",      "employee",   "update",    "Update employee records"),
        // Permissions
        ("permission:create",    "permission", "create",    "Create permissions"),
        ("permission:read",      "permission", "read",      "Read permissions"),
        ("permission:update",    "permission", "update",    "Update permissions"),
        ("permission:delete",    "permission", "delete",    "Delete permissions"),
        // Policies & Delegations
        ("policies:read",        "policies",   "read",      "Read ABAC policies"),
        ("policies:create",      "policies",   "create",    "Create ABAC policies"),
        ("policies:update",      "policies",   "update",    "Update ABAC policies"),
        ("policies:delete",      "policies",   "delete",    "Delete ABAC policies"),
        ("delegations:create",   "delegations","create",    "Create permission delegations"),
        ("delegations:read",     "delegations","read",      "Read delegations"),
        ("delegations:revoke",   "delegations","revoke",    "Revoke delegations"),
        // Audit
        ("audit:read",           "audit",      "read",      "Read audit logs"),
        ("audit:export",         "audit",      "export",    "Export audit logs to CSV"),
        // Tenant management
        ("tenant:read",          "tenant",     "read",      "Read tenant configuration"),
        ("tenant:update",        "tenant",     "update",    "Update tenant configuration"),
    };

    // ── Role → Permission matrix ──────────────────────────────────────────────

    private static readonly Dictionary<string, string[]> RolePermMatrix = new()
    {
        ["TenantAdmin"]       = PermissionDefs.Select(p => p.Code).ToArray(), // all
        ["SuperAdmin"]        = PermissionDefs.Select(p => p.Code).ToArray(),
        ["SecurityAdmin"]     = ["security:audit", "security:policy", "user:read", "audit:read", "audit:export", "policies:read"],
        ["BackendDeveloper"]  = ["repo:read", "repo:write", "repo:merge", "project:read", "project:update"],
        ["FrontendDeveloper"] = ["repo:read", "repo:write", "repo:merge", "project:read"],
        ["FullStackDeveloper"]= ["repo:read", "repo:write", "repo:merge", "project:read", "project:update", "infra:monitor"],
        ["TechLead"]          = ["repo:read", "repo:write", "repo:merge", "repo:admin", "project:read", "project:update", "project:deploy", "role:assign", "infra:monitor"],
        ["EngineeringManager"]= ["repo:read", "repo:admin", "project:read", "project:update", "project:create", "role:assign", "user:read", "infra:monitor"],
        ["QAEngineer"]        = ["repo:read", "project:read"],
        ["QALead"]            = ["repo:read", "repo:write", "project:read", "project:update"],
        ["DevOpsEngineer"]    = ["infra:deploy", "infra:rollback", "infra:monitor", "repo:read", "project:deploy"],
        ["SRE"]               = ["infra:deploy", "infra:rollback", "infra:monitor", "audit:read"],
        ["ProductManager"]    = ["project:read", "project:create", "project:update", "user:read"],
        ["Designer"]          = ["project:read"],
        ["SalesExecutive"]    = ["user:read", "project:read", "invoice:read"],
        ["MarketingManager"]  = ["user:read", "project:read"],
        ["HRManager"]         = ["employee:read", "employee:update", "user:read", "user:create"],
        ["FinanceManager"]    = ["invoice:read", "invoice:approve", "employee:read", "user:read"],
        ["SupportAgent"]      = ["user:read", "project:read", "audit:read"],
        ["SupportManager"]    = ["user:read", "user:update", "project:read", "audit:read", "role:assign"],
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Entry point
    // ─────────────────────────────────────────────────────────────────────────

    public async Task SeedAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[DataSeeder] ═══ FULL RESET + RESEED ═══");

        var sys = new SeedSuperAdminContext();

        await using var tenantDb    = new TenantDbContext(Opts<TenantDbContext>(), sys);
        await using var identityDb  = new IdentityDbContext(Opts<IdentityDbContext>(), sys);
        await using var rbacDb      = new RbacDbContext(Opts<RbacDbContext>(), sys);
        await using var policyDb    = new PolicyDbContext(Opts<PolicyDbContext>(), sys);
        await using var delegationDb= new DelegationDbContext(Opts<DelegationDbContext>(), sys);
        await using var auditDb     = new AuditDbContext(Opts<AuditDbContext>(), sys);
        var hasher                  = _provider.GetRequiredService<IPasswordHasher>();

        foreach (var (name, slug, domain) in TenantDefs)
        {
            _logger.LogInformation("[DataSeeder] ── Seeding tenant: {Name}", name);

            var tenantId = await SeedTenantsAsync(tenantDb, name, slug, ct);

            // Scopes must be committed before users & roles reference them
            var scopes = await SeedScopesAsync(rbacDb, tenantId, ct);
            rbacDb.ChangeTracker.Clear();

            var users = await SeedUsersAsync(identityDb, hasher, tenantId, domain, ct);
            identityDb.ChangeTracker.Clear();

            var roles = await SeedRolesAsync(rbacDb, tenantId, users.AdminId, ct);
            rbacDb.ChangeTracker.Clear();

            var perms = await SeedPermissionsAsync(rbacDb, tenantId, users.AdminId, ct);
            rbacDb.ChangeTracker.Clear();

            await SeedRolePermissionsAsync(rbacDb, tenantId, roles, perms, users.AdminId, ct);
            rbacDb.ChangeTracker.Clear();

            await SeedUserRoleAssignmentsAsync(rbacDb, tenantId, users, roles, scopes, ct);
            rbacDb.ChangeTracker.Clear();

            await SeedDelegationsAsync(delegationDb, tenantId, users, scopes, perms, ct);
            delegationDb.ChangeTracker.Clear();

            await SeedPoliciesAsync(policyDb, tenantId, users.AdminId, ct);
            policyDb.ChangeTracker.Clear();

            await SeedAuditLogsAsync(auditDb, tenantId, users, ct);
            auditDb.ChangeTracker.Clear();

            _logger.LogInformation(
                "[DataSeeder] ✓ {Name}: tenantId={Id}  admin=admin@{Domain}  pwd={Pwd}",
                name, tenantId, domain, SeedPassword);
        }

        _logger.LogInformation("[DataSeeder] ═══ DONE ═══");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. SeedTenantsAsync
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Guid> SeedTenantsAsync(
        TenantDbContext db, string name, string slug, CancellationToken ct)
    {
        var tenant = Tenant.Create(
            name,
            TenantManagement.Domain.ValueObjects.TenantSlug.Create(slug),
            createdByUserId: Guid.Empty);

        tenant.MarkBootstrapped(Guid.Empty);
        tenant.ClearDomainEvents();

        await db.Tenants.AddAsync(tenant, ct);
        await db.SaveChangesAsync(ct);

        return tenant.Id;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. SeedScopesAsync
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, Guid>> SeedScopesAsync(
        RbacDbContext db, Guid tenantId, CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>();

        // ScopeDefs is already ordered: parents before children.
        foreach (var (key, scopeName, type, parentKey) in ScopeDefs)
        {
            Guid? parentId = parentKey is not null ? result[parentKey] : null;
            var scope = Scope.Create(tenantId, scopeName, type, parentId, null, Guid.Empty);
            await db.Scopes.AddAsync(scope, ct);
            await db.SaveChangesAsync(ct);

            await InsertClosureRowsAsync(db, tenantId, scope.Id, parentId, ct);
            result[key] = scope.Id;
        }

        _logger.LogInformation("[DataSeeder] Seeded {Count} scopes", result.Count);
        return result;
    }

    private static async Task InsertClosureRowsAsync(
        RbacDbContext db, Guid tenantId, Guid newId, Guid? parentId, CancellationToken ct)
    {
        var rows = new List<ScopeHierarchy>
        {
            ScopeHierarchy.Create(tenantId, newId, newId, 0)
        };

        if (parentId.HasValue)
        {
            var ancestors = await db.ScopeHierarchies
                .IgnoreQueryFilters()
                .Where(sh => sh.DescendantId == parentId.Value && sh.TenantId == tenantId)
                .ToListAsync(ct);

            rows.AddRange(ancestors.Select(a =>
                ScopeHierarchy.Create(tenantId, a.AncestorId, newId, a.Depth + 1)));
        }

        await db.ScopeHierarchies.AddRangeAsync(rows, ct);
        await db.SaveChangesAsync(ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. SeedUsersAsync — ~300 department users + special accounts
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<TenantUsers> SeedUsersAsync(
        IdentityDbContext db,
        IPasswordHasher hasher,
        Guid tenantId,
        string domain,
        CancellationToken ct)
    {
        var (adminHash, adminSalt) = hasher.HashPassword(SeedPassword);

        // ── Fixed special accounts ───────────────────────────────────────────
        var admin     = await CreateUserAsync(db, hasher, tenantId, $"admin@{domain}",    "Tenant Admin",     ct);
        var techLead  = await CreateUserAsync(db, hasher, tenantId, $"techlead@{domain}", "Alex Tech Lead",   ct);
        var devMgr    = await CreateUserAsync(db, hasher, tenantId, $"devmgr@{domain}",   "Sam Dev Manager",  ct);
        var secAdmin  = await CreateUserAsync(db, hasher, tenantId, $"secadmin@{domain}", "Security Admin",   ct);
        var delegatee = await CreateUserAsync(db, hasher, tenantId, $"delegatee@{domain}","Dana Delegatee",   ct);
        await db.SaveChangesAsync(ct);

        // ── Department bulk users ─────────────────────────────────────────────
        var byDept = new Dictionary<string, List<Guid>>();
        int nameIdx = 0;
        var rng = new Random(42); // fixed seed for reproducibility

        foreach (var (scopeKey, count, _, _) in DeptDefs)
        {
            byDept[scopeKey] = new List<Guid>(count);

            for (int i = 0; i < count; i++)
            {
                int fi = nameIdx % FirstNames.Length;
                int li = (nameIdx / FirstNames.Length) % LastNames.Length;
                nameIdx++;

                var firstName = FirstNames[fi];
                var lastName  = LastNames[li];
                // Ensure uniqueness by appending index if the combo is reused
                var suffix    = nameIdx > FirstNames.Length * LastNames.Length ? $"{nameIdx}" : "";
                var email     = $"{firstName.ToLower()}.{lastName.ToLower()}{suffix}@{domain}";
                var display   = $"{firstName} {lastName}";

                bool inactive    = rng.Next(100) < 5;   // 5% inactive
                bool softDeleted = rng.Next(100) < 2;   // 2% soft-deleted
                bool anonymised  = rng.Next(100) < 1;   // 1% GDPR-erased

                var user = User.Create(
                    tenantId,
                    Identity.Domain.ValueObjects.Email.Create(email),
                    Identity.Domain.ValueObjects.DisplayName.Create(display),
                    createdByUserId: Guid.Empty);

                if (inactive && !softDeleted && !anonymised)
                    user.Deactivate(Guid.Empty, "Offboarded");

                if (anonymised)
                {
                    var token = $"erased-{Guid.NewGuid():N}";
                    user.Anonymise(token, Guid.Empty);
                }

                var (hash, salt) = hasher.HashPassword(SeedPassword);
                var cred = UserCredential.Create(user.Id, tenantId, hash, salt);

                await db.Users.AddAsync(user, ct);
                await db.UserCredentials.AddAsync(cred, ct);
                user.ClearDomainEvents();
                byDept[scopeKey].Add(user.Id);

                if (softDeleted)
                    user.SoftDelete(Guid.Empty);

                // Batch saves for performance
                if ((i + 1) % 50 == 0)
                    await db.SaveChangesAsync(ct);
            }

            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("[DataSeeder] Seeded {Count} users (domain={Domain})",
            5 + DeptDefs.Sum(d => d.Count), domain);

        return new TenantUsers(admin, techLead, devMgr, secAdmin, delegatee, byDept);
    }

    private static async Task<Guid> CreateUserAsync(
        IdentityDbContext db, IPasswordHasher hasher,
        Guid tenantId, string email, string display, CancellationToken ct)
    {
        var user = User.Create(
            tenantId,
            Identity.Domain.ValueObjects.Email.Create(email),
            Identity.Domain.ValueObjects.DisplayName.Create(display),
            createdByUserId: Guid.Empty);

        var (hash, salt) = hasher.HashPassword(SeedPassword);
        var cred = UserCredential.Create(user.Id, tenantId, hash, salt);

        await db.Users.AddAsync(user, ct);
        await db.UserCredentials.AddAsync(cred, ct);
        user.ClearDomainEvents();
        return user.Id;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. SeedRolesAsync
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, Guid>> SeedRolesAsync(
        RbacDbContext db, Guid tenantId, Guid adminId, CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>();

        foreach (var (name, desc, isSystem) in RoleDefs)
        {
            var role = Role.Create(tenantId, name, desc, adminId, isSystem);
            role.ClearDomainEvents();
            await db.Roles.AddAsync(role, ct);
            result[name] = role.Id;
        }

        // One soft-deleted legacy role — exercises IsDeleted filter
        var legacy = Role.Create(tenantId, "LegacyReadOnly", "Retired read-only role.", adminId, false);
        legacy.SoftDelete(adminId);
        legacy.ClearDomainEvents();
        await db.Roles.AddAsync(legacy, ct);

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[DataSeeder] Seeded {Count} roles", result.Count + 1);
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. SeedPermissionsAsync
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, Guid>> SeedPermissionsAsync(
        RbacDbContext db, Guid tenantId, Guid adminId, CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>();

        foreach (var (code, resource, action, desc) in PermissionDefs)
        {
            var perm = Permission.Create(tenantId, code, resource, action, desc, adminId);
            await db.Permissions.AddAsync(perm, ct);
            result[code] = perm.Id;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[DataSeeder] Seeded {Count} permissions", result.Count);
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. SeedRolePermissionsAsync — raw SQL to bypass Role aggregate
    // ─────────────────────────────────────────────────────────────────────────

    private async Task SeedRolePermissionsAsync(
        RbacDbContext db,
        Guid tenantId,
        Dictionary<string, Guid> roles,
        Dictionary<string, Guid> perms,
        Guid adminId,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var (roleName, codes) in RolePermMatrix)
        {
            if (!roles.TryGetValue(roleName, out var roleId)) continue;

            foreach (var code in codes)
            {
                if (!perms.TryGetValue(code, out var permId)) continue;

                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO rbac."RolePermissions"
                        ("Id","RoleId","PermissionId","TenantId","GrantedByUserId","GrantedAt")
                    VALUES
                        ({Guid.NewGuid()},{roleId},{permId},{tenantId},{adminId},{now})
                    ON CONFLICT DO NOTHING
                    """, ct);
            }
        }

        _logger.LogInformation("[DataSeeder] Role-permission assignments done");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. SeedUserRoleAssignmentsAsync
    // ─────────────────────────────────────────────────────────────────────────

    private async Task SeedUserRoleAssignmentsAsync(
        RbacDbContext db,
        Guid tenantId,
        TenantUsers users,
        Dictionary<string, Guid> roles,
        Dictionary<string, Guid> scopes,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var rng = new Random(42);

        void Assign(Guid userId, string roleName, Guid? scopeId,
                    DateTimeOffset? expiresAt = null, bool deactivate = false)
        {
            if (!roles.TryGetValue(roleName, out var roleId)) return;
            var a = UserRoleAssignment.Create(tenantId, userId, roleId, scopeId, expiresAt, users.AdminId);
            if (deactivate) a.Deactivate("Offboarded", users.AdminId);
            a.ClearDomainEvents();
            db.UserRoleAssignments.Add(a);
        }

        // Special accounts — tenant-wide
        Assign(users.AdminId,   "TenantAdmin",         null);
        Assign(users.SecAdminId,"SecurityAdmin",       null);
        Assign(users.TechLeadId,"TechLead",            scopes["eng"]);
        Assign(users.TechLeadId,"BackendDeveloper",    scopes["backend"]);
        Assign(users.DevMgrId,  "EngineeringManager",  scopes["eng"]);
        Assign(users.DevMgrId,  "TechLead",            scopes["backend"]); // multi-role

        // Delegatee has no direct role — used for delegation test
        // (intentionally left without assignment)

        // Department bulk assignments
        foreach (var (scopeKey, _, primaryRole, managerRole) in DeptDefs)
        {
            if (!scopes.TryGetValue(scopeKey, out var deptScope)) continue;
            if (!users.ByDept.TryGetValue(scopeKey, out var deptUsers)) continue;

            for (int i = 0; i < deptUsers.Count; i++)
            {
                var uid = deptUsers[i];
                bool isManager = managerRole is not null && i == 0;
                bool isLead    = i == 1;
                bool expiring  = rng.Next(100) < 8;   // 8% time-limited
                bool inactive  = rng.Next(100) < 3;   // 3% deactivated

                DateTimeOffset? exp = expiring
                    ? now.AddDays(rng.Next(10, 90))
                    : null;

                if (isManager && managerRole is not null)
                    Assign(uid, managerRole, deptScope, exp, inactive);
                else if (isLead && primaryRole is "BackendDeveloper" or "FrontendDeveloper")
                    Assign(uid, "TechLead", deptScope, exp, inactive);
                else
                    Assign(uid, primaryRole, deptScope, exp, inactive);

                // Some engineers also get FullStack
                if (primaryRole is "BackendDeveloper" or "FrontendDeveloper" && rng.Next(100) < 15)
                    Assign(uid, "FullStackDeveloper", deptScope);
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[DataSeeder] User-role assignments done");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. SeedDelegationsAsync
    // ─────────────────────────────────────────────────────────────────────────

    private async Task SeedDelegationsAsync(
        DelegationDbContext db,
        Guid tenantId,
        TenantUsers users,
        Dictionary<string, Guid> scopes,
        Dictionary<string, Guid> perms,
        CancellationToken ct)
    {
        // 1. ACTIVE — TechLead delegates deploy to a backend dev for 2 days
        var backendDev = users.ByDept["backend"].Count > 2 ? users.ByDept["backend"][2] : users.ByDept["backend"][0];

        var active = DelegationGrant.Create(
            tenantId,
            delegatorId: users.TechLeadId,
            delegateeId: backendDev,
            permissionCodes: ["project:deploy", "infra:monitor"],
            scopeId: scopes["backend"],
            expiresAt: DateTimeOffset.UtcNow.AddDays(2),
            chainDepth: 1,
            createdByUserId: users.TechLeadId);
        active.ClearDomainEvents();
        await db.Delegations.AddAsync(active, ct);

        // 2. ACTIVE — DevManager grants user:read to TechLead for HR visibility
        var mgr2lead = DelegationGrant.Create(
            tenantId,
            delegatorId: users.DevMgrId,
            delegateeId: users.TechLeadId,
            permissionCodes: ["user:read", "employee:read"],
            scopeId: scopes["eng"],
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            chainDepth: 1,
            createdByUserId: users.DevMgrId);
        mgr2lead.ClearDomainEvents();
        await db.Delegations.AddAsync(mgr2lead, ct);

        // 3. ACTIVE — TechLead delegates repo:read to delegatee (cross-team visibility)
        var toGuest = DelegationGrant.Create(
            tenantId,
            delegatorId: users.TechLeadId,
            delegateeId: users.DelegateeId,
            permissionCodes: ["repo:read", "project:read"],
            scopeId: scopes["org"],
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            chainDepth: 1,
            createdByUserId: users.TechLeadId);
        toGuest.ClearDomainEvents();
        await db.Delegations.AddAsync(toGuest, ct);

        // 4. REVOKED — DevManager tried to delegate finance:approve (revoked immediately)
        var frontendUser = users.ByDept["frontend"].Count > 0 ? users.ByDept["frontend"][0] : users.DevMgrId;
        var revoked = DelegationGrant.Create(
            tenantId,
            delegatorId: users.DevMgrId,
            delegateeId: frontendUser,
            permissionCodes: ["infra:deploy"],
            scopeId: scopes["devops"],
            expiresAt: DateTimeOffset.UtcNow.AddDays(14),
            chainDepth: 1,
            createdByUserId: users.DevMgrId);
        revoked.Revoke(revokedByUserId: users.AdminId);
        revoked.ClearDomainEvents();
        await db.Delegations.AddAsync(revoked, ct);

        // 5. EXPIRED — created with brief future window, backdated via raw SQL
        var expired = DelegationGrant.Create(
            tenantId,
            delegatorId: users.TechLeadId,
            delegateeId: users.DelegateeId,
            permissionCodes: ["policies:read"],
            scopeId: scopes["eng"],
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(10),
            chainDepth: 1,
            createdByUserId: users.TechLeadId);
        expired.ClearDomainEvents();
        await db.Delegations.AddAsync(expired, ct);
        await db.SaveChangesAsync(ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE delegation."Delegations" SET "ExpiresAt" = {DateTimeOffset.UtcNow.AddDays(-3)} WHERE "Id" = {expired.Id}""",
            ct);

        _logger.LogInformation("[DataSeeder] Seeded 5 delegations (3 active, 1 revoked, 1 expired)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. SeedPoliciesAsync
    // ─────────────────────────────────────────────────────────────────────────

    private async Task SeedPoliciesAsync(
        PolicyDbContext db, Guid tenantId, Guid adminId, CancellationToken ct)
    {
        var policies = new (string Name, string Desc, PolicyEffect Effect, string Json)[]
        {
            (
                "Deny Deploy Outside Business Hours",
                "Blocks infra:deploy when wall-clock is outside 08:00–18:00 UTC.",
                PolicyEffect.Deny,
                """{"operator":"Not","conditions":[{"attribute":"env.time_utc","op":"Between","value":["08:00","18:00"]}]}"""
            ),
            (
                "Engineering Department Only",
                "Allows repo and project access only for Engineering department users.",
                PolicyEffect.Allow,
                """{"operator":"And","conditions":[{"attribute":"user.department","op":"Eq","value":"Engineering"}]}"""
            ),
            (
                "Finance Approval: FinanceManager Only",
                "Denies invoice:approve for any user who is not FinanceManager.",
                PolicyEffect.Deny,
                """{"operator":"And","conditions":[{"attribute":"user.role","op":"Neq","value":"FinanceManager"}]}"""
            ),
            (
                "Internal Network Only",
                "Restricts access to requests originating from the 10.0.x.x internal subnet.",
                PolicyEffect.Allow,
                """{"operator":"And","conditions":[{"attribute":"env.ip","op":"StartsWith","value":"10.0."}]}"""
            ),
            (
                "Deny Tenant Config Updates for Non-Admins",
                "Prevents tenant:update for anyone without the platform-admin attribute.",
                PolicyEffect.Deny,
                """{"operator":"And","conditions":[{"attribute":"user.role","op":"Neq","value":"TenantAdmin"}]}"""
            ),
            (
                "Multi-Condition: Engineering + Hours + Internal",
                "Composite ABAC: Engineering department AND business hours AND internal IP.",
                PolicyEffect.Allow,
                """{"operator":"And","conditions":[{"attribute":"user.department","op":"Eq","value":"Engineering"},{"attribute":"env.time_utc","op":"Between","value":["08:00","18:00"]},{"attribute":"env.ip","op":"StartsWith","value":"10.0."}]}"""
            ),
            (
                "Security Team: Always Allow Audit",
                "SecurityAdmin can always read audit logs regardless of time or location.",
                PolicyEffect.Allow,
                """{"operator":"And","conditions":[{"attribute":"user.role","op":"Eq","value":"SecurityAdmin"}]}"""
            ),
            (
                "Sales: CRM Read During Business Hours",
                "Sales team gets CRM read access during business hours only.",
                PolicyEffect.Allow,
                """{"operator":"And","conditions":[{"attribute":"user.department","op":"Eq","value":"Sales"},{"attribute":"env.time_utc","op":"Between","value":["07:00","20:00"]}]}"""
            ),
        };

        foreach (var (name, desc, effect, json) in policies)
        {
            var p = Policy.Create(tenantId, name, desc, effect, json, null, null, adminId);
            p.ClearDomainEvents();
            await db.Policies.AddAsync(p, ct);
        }

        // Soft-deleted legacy policy — exercises IsDeleted filter
        var dead = Policy.Create(
            tenantId,
            "DELETED: Old VPN Restriction",
            "Retired — kept to exercise soft-delete filtering.",
            PolicyEffect.Deny,
            """{"operator":"And","conditions":[{"attribute":"env.ip","op":"StartsWith","value":"192.168."}]}""",
            null, null, adminId);
        dead.SoftDelete(adminId);
        dead.ClearDomainEvents();
        await db.Policies.AddAsync(dead, ct);

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[DataSeeder] Seeded {Count} policies (+1 soft-deleted)", policies.Length);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. SeedAuditLogsAsync
    // ─────────────────────────────────────────────────────────────────────────

    private async Task SeedAuditLogsAsync(
        AuditDbContext db, Guid tenantId, TenantUsers users, CancellationToken ct)
    {
        var correlation = Guid.NewGuid();
        var resA = Guid.NewGuid();
        var resB = Guid.NewGuid();
        var sc   = Guid.NewGuid();

        var backendDev = users.ByDept["backend"].Count > 0 ? users.ByDept["backend"][0] : users.AdminId;
        var frontDev   = users.ByDept["frontend"].Count > 0 ? users.ByDept["frontend"][0] : users.AdminId;
        var salesUser  = users.ByDept["sales"].Count > 0 ? users.ByDept["sales"][0] : users.AdminId;

        var logs = new List<AuditLog>
        {
            // ── Access decisions ──────────────────────────────────────────────

            AuditLog.CreateAccessDecision(tenantId, correlation, users.AdminId,
                "tenant:read", resA, sc,
                isGranted: true, denialReason: null,
                cacheHit: true, evaluationLatencyMs: 1, policyId: null, delegationChain: null),

            AuditLog.CreateAccessDecision(tenantId, Guid.NewGuid(), backendDev,
                "repo:write", resA, sc,
                isGranted: true, denialReason: null,
                cacheHit: false, evaluationLatencyMs: 18, policyId: null, delegationChain: null),

            AuditLog.CreateAccessDecision(tenantId, Guid.NewGuid(), backendDev,
                "project:deploy", resB, sc,
                isGranted: true, denialReason: null,
                cacheHit: false, evaluationLatencyMs: 32,
                policyId: null,
                delegationChain: $"{users.TechLeadId}→{backendDev}"),

            AuditLog.CreateAccessDecision(tenantId, Guid.NewGuid(), frontDev,
                "infra:deploy", resA, sc,
                isGranted: false, denialReason: "NoPermissionFound",
                cacheHit: false, evaluationLatencyMs: 12, policyId: null, delegationChain: null),

            AuditLog.CreateAccessDecision(tenantId, Guid.NewGuid(), salesUser,
                "repo:write", resB, sc,
                isGranted: false, denialReason: "NoPermissionFound",
                cacheHit: false, evaluationLatencyMs: 9, policyId: null, delegationChain: null),

            AuditLog.CreateAccessDecision(tenantId, Guid.NewGuid(), backendDev,
                "infra:deploy", resA, sc,
                isGranted: false, denialReason: "AbacConditionFailed",
                cacheHit: false, evaluationLatencyMs: 47,
                policyId: "Deny Deploy Outside Business Hours", delegationChain: null),

            AuditLog.CreateAccessDecision(tenantId, Guid.NewGuid(), users.DelegateeId,
                "repo:read", resA, sc,
                isGranted: true, denialReason: null,
                cacheHit: false, evaluationLatencyMs: 25,
                policyId: null,
                delegationChain: $"{users.TechLeadId}→{users.DelegateeId}"),

            AuditLog.CreateAccessDecision(tenantId, Guid.NewGuid(), users.DelegateeId,
                "policies:read", resB, sc,
                isGranted: false, denialReason: "DelegationExpired",
                cacheHit: false, evaluationLatencyMs: 8,
                policyId: null,
                delegationChain: $"{users.TechLeadId}→{users.DelegateeId}"),

            AuditLog.CreateAccessDecision(tenantId, Guid.NewGuid(), users.SecAdminId,
                "security:audit", resA, sc,
                isGranted: true, denialReason: null,
                cacheHit: true, evaluationLatencyMs: 2,
                policyId: "Security Team: Always Allow Audit", delegationChain: null),

            AuditLog.CreateAccessDecision(tenantId, Guid.NewGuid(), frontDev,
                "tenant:update", resA, sc,
                isGranted: false, denialReason: "ExplicitGlobalDeny",
                cacheHit: false, evaluationLatencyMs: 6,
                policyId: "Deny Tenant Config Updates for Non-Admins", delegationChain: null),

            // ── Admin actions ─────────────────────────────────────────────────

            AuditLog.CreateAdminAction(tenantId, correlation, users.AdminId,
                "role:create", "Role", Guid.NewGuid(),
                oldValue: null,
                newValue: """{"name":"BackendDeveloper","isSystem":false}"""),

            AuditLog.CreateAdminAction(tenantId, Guid.NewGuid(), users.AdminId,
                "user:create", "User", backendDev,
                oldValue: null,
                newValue: $$$"""{"email":"backend.dev@domain","tenantId":"{{{tenantId}}}"}"""),

            AuditLog.CreateAdminAction(tenantId, Guid.NewGuid(), users.AdminId,
                "role:assign", "UserRoleAssignment", Guid.NewGuid(),
                oldValue: null,
                newValue: $$$"""{"userId":"{{{backendDev}}}","role":"BackendDeveloper","scope":"backend"}"""),

            AuditLog.CreateAdminAction(tenantId, Guid.NewGuid(), users.AdminId,
                "policies:create", "Policy", Guid.NewGuid(),
                oldValue: null,
                newValue: """{"name":"Deny Deploy Outside Business Hours","effect":"Deny"}"""),

            AuditLog.CreateAdminAction(tenantId, Guid.NewGuid(), users.AdminId,
                "delegations:revoke", "Delegation", Guid.NewGuid(),
                oldValue: $$$"""{"status":"Active","delegateeId":"{{{users.DelegateeId}}}"}""",
                newValue: """{"status":"Revoked","reason":"Security review"}"""),

            AuditLog.CreateAdminAction(tenantId, Guid.NewGuid(), users.SecAdminId,
                "security:policy", "Policy", Guid.NewGuid(),
                oldValue: """{"effect":"Allow"}""",
                newValue: """{"effect":"Deny","reason":"Compliance audit 2024"}"""),

            AuditLog.CreateAdminAction(tenantId, Guid.NewGuid(), users.DevMgrId,
                "role:assign", "UserRoleAssignment", Guid.NewGuid(),
                oldValue: null,
                newValue: $$$"""{"userId":"{{{users.TechLeadId}}}","role":"TechLead","scope":"eng"}"""),
        };

        foreach (var log in logs)
            await db.AuditLogs.AddAsync(log, ct);

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[DataSeeder] Seeded {Count} audit log entries", logs.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private DbContextOptions<T> Opts<T>() where T : DbContext
        => _provider.GetRequiredService<DbContextOptions<T>>();
}

// ─────────────────────────────────────────────────────────────────────────────
// Supporting types
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>All user IDs produced for a single tenant by SeedUsersAsync.</summary>
internal sealed record TenantUsers(
    Guid AdminId,
    Guid TechLeadId,
    Guid DevMgrId,
    Guid SecAdminId,
    Guid DelegateeId,
    Dictionary<string, List<Guid>> ByDept);

/// <summary>
/// Bypasses every EF Core global query filter during seeding.
/// Never use this outside of DataSeeder.
/// </summary>
file sealed class SeedSuperAdminContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
    public bool IsSuperAdmin => true;
    public Guid UserId => Guid.Empty;
}
