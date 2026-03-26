using BuildingBlocks.Application;
using RbacCore.Application.Common;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Queries;

public sealed class GetEffectivePermissionsQueryHandler
    : IQueryHandler<GetEffectivePermissionsQuery, IReadOnlyList<PermissionDto>>
{
    private readonly IUserRoleAssignmentRepository _assignmentRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IScopeRepository _scopeRepository;

    public GetEffectivePermissionsQueryHandler(
        IUserRoleAssignmentRepository assignmentRepository,
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IScopeRepository scopeRepository)
    {
        _assignmentRepository = assignmentRepository;
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _scopeRepository = scopeRepository;
    }

    public async Task<IReadOnlyList<PermissionDto>> Handle(
        GetEffectivePermissionsQuery query,
        CancellationToken cancellationToken)
    {
        // Resolve scope chain: current scope + all ancestors (upward inheritance)
        var scopeIds = new List<Guid?> { query.ScopeId };

        if (query.ScopeId.HasValue)
        {
            var ancestorIds = await _scopeRepository.GetAncestorIdsAsync(
                query.ScopeId.Value, query.TenantId, cancellationToken);

            scopeIds.AddRange(ancestorIds.Select(id => (Guid?)id));
            scopeIds.Add(null); // null scope = tenant-wide assignments
        }

        var permissionIds = new HashSet<Guid>();

        foreach (var scopeId in scopeIds.Distinct())
        {
            var assignments = await _assignmentRepository.GetActiveByUserAsync(
                query.UserId, query.TenantId, scopeId, cancellationToken);

            foreach (var assignment in assignments)
            {
                var role = await _roleRepository.GetByIdAsync(
                    assignment.RoleId, cancellationToken);

                if (role is null) continue;

                foreach (var rp in role.Permissions)
                    permissionIds.Add(rp.PermissionId);
            }
        }

        if (permissionIds.Count == 0)
            return [];

        var permissions = await _permissionRepository.GetByTenantAsync(
            query.TenantId, cancellationToken);

        return permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => new PermissionDto(p.Id, p.TenantId, p.Code.Value, p.ResourceType, p.Action, p.Description))
            .ToList()
            .AsReadOnly();
    }
}
