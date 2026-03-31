using BuildingBlocks.Application;
using RbacCore.Application.Common;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Queries;

public sealed class ListRolesQueryHandler
    : IQueryHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;

    public ListRolesQueryHandler(IRoleRepository roleRepository)
        => _roleRepository = roleRepository;

    public async Task<IReadOnlyList<RoleDto>> Handle(
        ListRolesQuery query,
        CancellationToken cancellationToken)
    {
        var roles = await _roleRepository.GetByTenantAsync(query.TenantId, cancellationToken);

        return roles
            .Select(r => new RoleDto(
                r.Id, r.TenantId, r.Name, r.Description,
                r.IsSystemRole, r.CreatedAt,
                r.Permissions
                    .Select(rp => new PermissionDto(rp.PermissionId, r.TenantId, string.Empty, string.Empty, string.Empty, null))
                    .ToList()))
            .ToList()
            .AsReadOnly();
    }
}
