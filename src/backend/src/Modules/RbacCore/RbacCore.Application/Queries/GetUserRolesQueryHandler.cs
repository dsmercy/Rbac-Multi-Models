using BuildingBlocks.Application;
using RbacCore.Application.Common;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Queries;

public sealed class GetUserRolesQueryHandler
    : IQueryHandler<GetUserRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly IUserRoleAssignmentRepository _assignmentRepository;
    private readonly IRoleRepository _roleRepository;

    public GetUserRolesQueryHandler(
        IUserRoleAssignmentRepository assignmentRepository,
        IRoleRepository roleRepository)
    {
        _assignmentRepository = assignmentRepository;
        _roleRepository = roleRepository;
    }

    public async Task<IReadOnlyList<RoleDto>> Handle(
        GetUserRolesQuery query,
        CancellationToken cancellationToken)
    {
        var assignments = await _assignmentRepository.GetActiveByUserAsync(
            query.UserId, query.TenantId, query.ScopeId, cancellationToken);

        var roles = new List<RoleDto>();

        foreach (var assignment in assignments)
        {
            var role = await _roleRepository.GetByIdAsync(assignment.RoleId, cancellationToken);
            if (role is null) continue;

            var permissions = role.Permissions
                .Select(rp => new PermissionDto(
                    rp.PermissionId,
                    query.TenantId,
                    string.Empty, string.Empty, string.Empty, null))
                .ToList();

            roles.Add(new RoleDto(
                role.Id, role.TenantId, role.Name,
                role.Description, role.IsSystemRole, role.CreatedAt, permissions));
        }

        return roles.AsReadOnly();
    }
}
