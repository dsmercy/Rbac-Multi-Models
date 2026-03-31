using BuildingBlocks.Application;
using RbacCore.Application.Common;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Queries;

public sealed class GetRolePermissionsQueryHandler
    : IQueryHandler<GetRolePermissionsQuery, IReadOnlyList<PermissionDto>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;

    public GetRolePermissionsQueryHandler(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
    }

    public async Task<IReadOnlyList<PermissionDto>> Handle(
        GetRolePermissionsQuery query,
        CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(query.RoleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Role {query.RoleId} not found.");

        if (role.TenantId != query.TenantId)
            throw new UnauthorizedAccessException("Role does not belong to the specified tenant.");

        if (role.Permissions.Count == 0)
            return [];

        var allPermissions = await _permissionRepository.GetByTenantAsync(query.TenantId, cancellationToken);
        var permissionMap = allPermissions.ToDictionary(p => p.Id);

        return role.Permissions
            .Where(rp => permissionMap.ContainsKey(rp.PermissionId))
            .Select(rp =>
            {
                var p = permissionMap[rp.PermissionId];
                return new PermissionDto(p.Id, p.TenantId, p.Code.Value, p.ResourceType, p.Action, p.Description);
            })
            .ToList()
            .AsReadOnly();
    }
}
