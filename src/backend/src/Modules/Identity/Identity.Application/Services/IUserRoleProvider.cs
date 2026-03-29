// Identity.Application/Services/IUserRoleProvider.cs
namespace Identity.Application.Services;

/// <summary>
/// Abstracts role name lookup during login.
/// Implemented in Identity.Infrastructure using direct DB queries
/// to avoid a circular dependency with RbacCore.Application.
/// </summary>
public interface IUserRoleProvider
{
    /// <summary>
    /// Returns role names for a user across both their tenant and the
    /// platform tenant (Guid.Empty) — used to bake roles into the JWT.
    /// Must bypass EF global query filters — no JWT exists during login.
    /// </summary>
    Task<IReadOnlyList<string>> GetRoleNamesForLoginAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default);
}