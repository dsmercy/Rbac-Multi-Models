using RbacCore.Application.Common;

namespace RbacCore.Application.Services;

/// <summary>
/// Public anti-corruption interface exposed to PermissionEngine and other modules.
/// No module may reference RbacCore.Infrastructure or RbacCore.Domain directly.
/// </summary>
public interface IRbacCoreService
{
    Task<IReadOnlyList<RoleDto>> GetUserRolesAsync(
        Guid userId, Guid tenantId, Guid? scopeId, CancellationToken ct = default);

    Task<IReadOnlyList<PermissionDto>> GetRolePermissionsAsync(
        Guid roleId, Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<PermissionDto>> GetEffectivePermissionsAsync(
        Guid userId, Guid tenantId, Guid? scopeId, CancellationToken ct = default);

    Task<bool> RoleExistsAsync(
        Guid roleId, Guid tenantId, CancellationToken ct = default);

    Task<bool> UserHasPermissionAsync(
        Guid userId, string permissionCode, Guid tenantId, Guid? scopeId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetAncestorScopeIdsAsync(
        Guid scopeId, Guid tenantId, CancellationToken ct = default);
}
