using BuildingBlocks.Application;
using RbacCore.Application.Common;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Queries;

public sealed class GetRoleByIdQueryHandler
    : IQueryHandler<GetRoleByIdQuery, RoleDto?>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;

    public GetRoleByIdQueryHandler(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
    }

    public async Task<RoleDto?> Handle(
        GetRoleByIdQuery query,
        CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(query.RoleId, cancellationToken);

        if (role is null || role.TenantId != query.TenantId)
            return null;

        var permissionIds = role.Permissions.Select(rp => rp.PermissionId).ToList();
        var allPermissions = await _permissionRepository.GetByTenantAsync(query.TenantId, cancellationToken);
        var permissionMap = allPermissions.ToDictionary(p => p.Id);

        var permissionDtos = permissionIds
            .Where(id => permissionMap.ContainsKey(id))
            .Select(id =>
            {
                var p = permissionMap[id];
                return new PermissionDto(p.Id, p.TenantId, p.Code.Value, p.ResourceType, p.Action, p.Description);
            })
            .ToList();

        return new RoleDto(
            role.Id, role.TenantId, role.Name, role.Description,
            role.IsSystemRole, role.CreatedAt, permissionDtos);
    }
}
