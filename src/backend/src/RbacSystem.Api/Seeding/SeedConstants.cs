namespace RbacSystem.Api.Seeding;

/// <summary>
/// Deterministic GUIDs for every seed entity.
/// Fixed values make the seeder idempotent across restarts —
/// existence checks use these IDs, so re-running never duplicates data.
///
/// Password for ALL test users: Test@1234567890!
/// </summary>
public static class SeedConstants
{
    // ──────────────────────────────────────────────────────────────────────────
    // Tenants
    // ──────────────────────────────────────────────────────────────────────────
    public static readonly Guid TenantAcmeId   = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid TenantTechId   = new("10000000-0000-0000-0000-000000000002");

    // ──────────────────────────────────────────────────────────────────────────
    // Users — Acme Corp
    // ──────────────────────────────────────────────────────────────────────────
    public static readonly Guid UserAcmeAdminId   = new("20000000-0000-0000-0000-000000000001");
    public static readonly Guid UserAcmeAliceId   = new("20000000-0000-0000-0000-000000000002"); // editor
    public static readonly Guid UserAcmeBobId     = new("20000000-0000-0000-0000-000000000003"); // viewer + delegatee
    public static readonly Guid UserAcmeCharlieId = new("20000000-0000-0000-0000-000000000004"); // auditor
    public static readonly Guid UserAcmeDaveId    = new("20000000-0000-0000-0000-000000000005"); // no role (negative test)

    // Users — TechStart
    public static readonly Guid UserTechAdminId   = new("20000000-0000-0000-0000-000000000011");
    public static readonly Guid UserTechDanaId    = new("20000000-0000-0000-0000-000000000012"); // editor

    // ──────────────────────────────────────────────────────────────────────────
    // Roles — Acme Corp  (tenant-admin is created by bootstrap, referenced here)
    // ──────────────────────────────────────────────────────────────────────────
    public static readonly Guid RoleAcmeTenantAdminId     = new("30000000-0000-0000-0000-000000000001");
    public static readonly Guid RoleAcmeEditorId          = new("30000000-0000-0000-0000-000000000002");
    public static readonly Guid RoleAcmeViewerId          = new("30000000-0000-0000-0000-000000000003");
    public static readonly Guid RoleAcmeAuditorId         = new("30000000-0000-0000-0000-000000000004");
    public static readonly Guid RoleAcmeDelegationUserId  = new("30000000-0000-0000-0000-000000000005");

    // Roles — TechStart
    public static readonly Guid RoleTechTenantAdminId     = new("30000000-0000-0000-0000-000000000011");
    public static readonly Guid RoleTechEditorId          = new("30000000-0000-0000-0000-000000000012");

    // ──────────────────────────────────────────────────────────────────────────
    // Scopes — Acme Corp
    // Hierarchy:
    //   AcmeOrg (org)
    //   ├── Engineering (dept)
    //   │   ├── AlphaProject (project)
    //   │   └── BetaProject  (project)
    //   └── Marketing (dept)
    //       └── CampaignQ1  (project)
    // ──────────────────────────────────────────────────────────────────────────
    public static readonly Guid ScopeAcmeOrgId         = new("40000000-0000-0000-0000-000000000001");
    public static readonly Guid ScopeAcmeEngId         = new("40000000-0000-0000-0000-000000000002");
    public static readonly Guid ScopeAcmeAlphaId       = new("40000000-0000-0000-0000-000000000003");
    public static readonly Guid ScopeAcmeBetaId        = new("40000000-0000-0000-0000-000000000004");
    public static readonly Guid ScopeAcmeMktId         = new("40000000-0000-0000-0000-000000000005");
    public static readonly Guid ScopeAcmeCampaignQ1Id  = new("40000000-0000-0000-0000-000000000006");

    // ──────────────────────────────────────────────────────────────────────────
    // Permissions — Acme Corp  (subset; full list in DefaultPermissions)
    // These IDs are used for RolePermission & UserRoleAssignment seeding.
    // Actual Permission rows are seeded dynamically from DefaultPermissions.GetAll()
    // with a deterministic ID derived from TenantId + Code.
    // ──────────────────────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────────────────────
    // Policies — Acme Corp
    // ──────────────────────────────────────────────────────────────────────────
    public static readonly Guid PolicyAcmeBusinessHoursId     = new("50000000-0000-0000-0000-000000000001");
    public static readonly Guid PolicyAcmeClassifiedDenyId    = new("50000000-0000-0000-0000-000000000002");
    public static readonly Guid PolicyAcmeBetaLockId          = new("50000000-0000-0000-0000-000000000003");
    public static readonly Guid PolicyAcmeSeniorOnlyId        = new("50000000-0000-0000-0000-000000000004");

    // ──────────────────────────────────────────────────────────────────────────
    // Delegations — Acme Corp
    // ──────────────────────────────────────────────────────────────────────────
    public static readonly Guid DelegationAliceToBobActiveId  = new("60000000-0000-0000-0000-000000000001"); // alice→bob users:read, active
    public static readonly Guid DelegationAliceToBobRevokedId = new("60000000-0000-0000-0000-000000000002"); // alice→bob roles:read, revoked
    public static readonly Guid DelegationCharlieToAliceExpId = new("60000000-0000-0000-0000-000000000003"); // charlie→alice audit-logs:read, expired

    // ──────────────────────────────────────────────────────────────────────────
    // Shared test password (plaintext — seeder hashes it via Pbkdf2PasswordHasher)
    // ──────────────────────────────────────────────────────────────────────────
    public const string TestPassword = "Test@1234567890!";

    // ──────────────────────────────────────────────────────────────────────────
    // Fake resource IDs used in permission-check tests and audit log entries
    // ──────────────────────────────────────────────────────────────────────────
    public static readonly Guid ResourceUserListId    = new("70000000-0000-0000-0000-000000000001");
    public static readonly Guid ResourceRoleListId    = new("70000000-0000-0000-0000-000000000002");
    public static readonly Guid ResourceBetaReportId  = new("70000000-0000-0000-0000-000000000003"); // locked by policy
    public static readonly Guid ResourceAuditExportId = new("70000000-0000-0000-0000-000000000004");
}
