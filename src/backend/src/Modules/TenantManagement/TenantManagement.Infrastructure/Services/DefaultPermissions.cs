namespace TenantManagement.Infrastructure.Services;

/// <summary>
/// Canonical list of default system permissions seeded on every new tenant.
/// Format: resource:action
/// </summary>
public static class DefaultPermissions
{
    // User management
    public const string UsersRead = "users:read";
    public const string UsersCreate = "users:create";
    public const string UsersUpdate = "users:update";
    public const string UsersDelete = "users:delete";

    // Role management
    public const string RolesRead = "roles:read";
    public const string RolesCreate = "roles:create";
    public const string RolesUpdate = "roles:update";
    public const string RolesDelete = "roles:delete";
    public const string RolesAssign = "roles:assign";
    public const string RolesRevoke = "roles:revoke";

    // Permission management
    public const string PermissionsRead = "permissions:read";
    public const string PermissionsGrant = "permissions:grant";
    public const string PermissionsRevoke = "permissions:revoke";

    // Policy management
    public const string PoliciesRead = "policies:read";
    public const string PoliciesCreate = "policies:create";
    public const string PoliciesUpdate = "policies:update";
    public const string PoliciesDelete = "policies:delete";

    // Delegation management
    public const string DelegationsRead = "delegations:read";
    public const string DelegationsCreate = "delegations:create";
    public const string DelegationsRevoke = "delegations:revoke";

    // Audit log access
    public const string AuditLogsRead = "audit-logs:read";
    public const string AuditLogsExport = "audit-logs:export";

    // Tenant configuration
    public const string TenantConfigRead = "tenant-config:read";
    public const string TenantConfigUpdate = "tenant-config:update";

    public static IReadOnlyList<string> GetAll() =>
    [
        UsersRead, UsersCreate, UsersUpdate, UsersDelete,
        RolesRead, RolesCreate, RolesUpdate, RolesDelete, RolesAssign, RolesRevoke,
        PermissionsRead, PermissionsGrant, PermissionsRevoke,
        PoliciesRead, PoliciesCreate, PoliciesUpdate, PoliciesDelete,
        DelegationsRead, DelegationsCreate, DelegationsRevoke,
        AuditLogsRead, AuditLogsExport,
        TenantConfigRead, TenantConfigUpdate,
    ];
}
