using MediatR;
using RbacCore.Application.Common;
using RbacCore.Application.Queries;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Services;

public sealed class RbacCoreService : IRbacCoreService
{
    private readonly ISender _sender;
    private readonly IRoleRepository _roleRepository;
    private readonly IScopeRepository _scopeRepository;

    public RbacCoreService(
        ISender sender,
        IRoleRepository roleRepository,
        IScopeRepository scopeRepository)
    {
        _sender = sender;
        _roleRepository = roleRepository;
        _scopeRepository = scopeRepository;
    }

    public Task<IReadOnlyList<RoleDto>> GetUserRolesAsync(
        Guid userId, Guid tenantId, Guid? scopeId, CancellationToken ct = default)
        => _sender.Send(new GetUserRolesQuery(userId, tenantId, scopeId), ct);

    public async Task<IReadOnlyList<PermissionDto>> GetRolePermissionsAsync(
        Guid roleId, Guid tenantId, CancellationToken ct = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId, ct);
        if (role is null || role.TenantId != tenantId) return [];

        return role.Permissions
            .Select(rp => new PermissionDto(
                rp.PermissionId, tenantId,
                string.Empty, string.Empty, string.Empty, null))
            .ToList()
            .AsReadOnly();
    }

    public Task<IReadOnlyList<PermissionDto>> GetEffectivePermissionsAsync(
        Guid userId, Guid tenantId, Guid? scopeId, CancellationToken ct = default)
        => _sender.Send(new GetEffectivePermissionsQuery(userId, tenantId, scopeId), ct);

    public Task<bool> RoleExistsAsync(
        Guid roleId, Guid tenantId, CancellationToken ct = default)
        => _roleRepository.ExistsAsync(roleId, tenantId, ct);

    public async Task<bool> UserHasPermissionAsync(
        Guid userId, string permissionCode, Guid tenantId, Guid? scopeId, CancellationToken ct = default)
    {
        var permissions = await _sender.Send(
            new GetEffectivePermissionsQuery(userId, tenantId, scopeId), ct);

        return permissions.Any(p =>
            p.Code.Equals(permissionCode, StringComparison.OrdinalIgnoreCase));
    }

    public Task<IReadOnlyList<Guid>> GetAncestorScopeIdsAsync(
        Guid scopeId, Guid tenantId, CancellationToken ct = default)
        => _scopeRepository.GetAncestorIdsAsync(scopeId, tenantId, ct);
}
