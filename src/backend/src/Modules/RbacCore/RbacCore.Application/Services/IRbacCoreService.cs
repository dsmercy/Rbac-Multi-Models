using RbacCore.Application.Common;

namespace RbacCore.Application.Services;

/// <summary>
/// Public anti-corruption interface exposed to other modules (PermissionEngine,
/// Delegation, TenantManagement). Callers must never reference RbacCore.Domain
/// or RbacCore.Infrastructure directly.
/// </summary>
public interface IRbacCoreService
{
    /// <summary>Returns all active role assignments for a user within a given scope (null = tenant-wide).</summary>
    Task<IReadOnlyList<UserRoleAssignmentDto>> GetUserRolesAsync(
        Guid userId, Guid tenantId, Guid? scopeId, CancellationToken ct = default);

    /// <summary>Returns all permissions attached to a role.</summary>
    Task<IReadOnlyList<PermissionDto>> GetRolePermissionsAsync(
        Guid roleId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns the union of all permissions held by the user after walking the
    /// scope hierarchy upward from the specified scope (null = tenant-wide).
    /// This is the hot-path called by the PermissionEngine on every evaluation.
    /// </summary>
    Task<IReadOnlyList<PermissionDto>> GetEffectivePermissionsAsync(
        Guid userId, Guid tenantId, Guid? scopeId, CancellationToken ct = default);

    /// <summary>Checks whether the user holds a specific permission at the given scope.</summary>
    Task<bool> UserHasPermissionAsync(
        Guid userId, string permissionCode, Guid tenantId, Guid? scopeId,
        CancellationToken ct = default);

    /// <summary>Returns all ancestor scope IDs for a given scope (closest first).</summary>
    Task<IReadOnlyList<Guid>> GetAncestorScopeIdsAsync(
        Guid scopeId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Fast existence check � used for assignment validation.</summary>
    Task<bool> RoleExistsAsync(
        Guid roleId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Used exclusively during login to bake roles into the JWT.
    /// Bypasses EF global query filters because no JWT exists at this point.
    /// Includes platform-tenant roles (Guid.Empty) for super-admins.
    /// </summary>
    Task<IReadOnlyList<string>> GetRoleNamesForLoginAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default);
}