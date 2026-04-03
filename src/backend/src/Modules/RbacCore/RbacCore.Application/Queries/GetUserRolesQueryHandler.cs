using BuildingBlocks.Application;
using RbacCore.Application.Common;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Queries;

public sealed class GetUserRolesQueryHandler
    : IQueryHandler<GetUserRolesQuery, IReadOnlyList<UserRoleAssignmentDto>>
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

    public async Task<IReadOnlyList<UserRoleAssignmentDto>> Handle(
        GetUserRolesQuery query,
        CancellationToken cancellationToken)
    {
        // When ScopeId is null (no filter requested), return ALL assignments across every scope.
        // GetActiveByUserAsync with null would only return ScopeId IS NULL (tenant-wide) records.
        var assignments = query.ScopeId.HasValue
            ? await _assignmentRepository.GetActiveByUserAsync(query.UserId, query.TenantId, query.ScopeId, cancellationToken)
            : await _assignmentRepository.GetAllActiveByUserAsync(query.UserId, query.TenantId, cancellationToken);

        var result = new List<UserRoleAssignmentDto>();

        foreach (var assignment in assignments)
        {
            var role = await _roleRepository.GetByIdAsync(assignment.RoleId, cancellationToken);

            result.Add(new UserRoleAssignmentDto(
                assignment.Id,
                assignment.TenantId,
                assignment.UserId,
                assignment.RoleId,
                role?.Name ?? string.Empty,
                assignment.ScopeId,
                assignment.IsActive,
                assignment.ExpiresAt,
                assignment.CreatedAt));
        }

        return result.AsReadOnly();
    }
}
