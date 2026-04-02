using BuildingBlocks.Application;
using RbacCore.Application.Common;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Queries;

public sealed class ListPermissionsQueryHandler
    : IQueryHandler<ListPermissionsQuery, IReadOnlyList<PermissionDto>>
{
    private readonly IPermissionRepository _permissionRepository;

    public ListPermissionsQueryHandler(IPermissionRepository permissionRepository)
        => _permissionRepository = permissionRepository;

    public async Task<IReadOnlyList<PermissionDto>> Handle(
        ListPermissionsQuery query,
        CancellationToken cancellationToken)
    {
        var permissions = await _permissionRepository.GetByTenantAsync(query.TenantId, cancellationToken);

        return permissions
            .Select(p => new PermissionDto(
                p.Id, p.TenantId, p.Code.Value, p.ResourceType, p.Action, p.Description))
            .ToList()
            .AsReadOnly();
    }
}
