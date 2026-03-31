namespace Identity.Application.Services;

/// <summary>
/// Full login context resolved during JWT generation.
/// All fields are baked into the access token claims at issuance.
/// </summary>
/// <param name="RoleNames">
///   Distinct role names across the user's tenant and the platform tenant
///   (Guid.Empty). Baked into the JWT "roles" claim.
/// </param>
/// <param name="ScopeIds">
///   Distinct scope IDs where the user has at least one active role
///   assignment. Baked into the JWT "scp" claim as "scope-type:{scopeId}".
/// </param>
/// <param name="IsSuperAdmin">
///   True when the user holds the "platform:super-admin" role.
///   Baked into the "is_super_admin" claim. Used by TenantValidationMiddleware
///   and EF Core query-filter bypass logic.
/// </param>
public sealed record UserLoginInfo(
    IReadOnlyList<string> RoleNames,
    IReadOnlyList<Guid> ScopeIds,
    bool IsSuperAdmin);

/// <summary>
/// Abstracts role and scope lookup during login.
///
/// Implemented in Identity.Infrastructure using a direct Dapper query against
/// the RbacCore connection string to avoid a circular dependency with
/// RbacCore.Application (which depends on Identity.Application).
///
/// Must bypass EF global query filters — no JWT exists during login.
/// </summary>
public interface IUserRoleProvider
{
    /// <summary>
    /// Returns all role names, scope IDs, and super-admin flag for a user
    /// across both their own tenant and the platform tenant (Guid.Empty).
    /// Used to embed claims in the issued JWT.
    /// </summary>
    Task<UserLoginInfo> GetLoginInfoAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default);
}
