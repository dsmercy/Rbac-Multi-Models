using BuildingBlocks.Application;
using Identity.Application.Services;
using RbacCore.Application.Common;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Queries;

public sealed class ListRoleMembersQueryHandler
    : IQueryHandler<ListRoleMembersQuery, IReadOnlyList<RoleMemberDto>>
{
    private readonly IUserRoleAssignmentRepository _assignmentRepository;
    private readonly IIdentityService _identityService;

    public ListRoleMembersQueryHandler(
        IUserRoleAssignmentRepository assignmentRepository,
        IIdentityService identityService)
    {
        _assignmentRepository = assignmentRepository;
        _identityService = identityService;
    }

    public async Task<IReadOnlyList<RoleMemberDto>> Handle(
        ListRoleMembersQuery query,
        CancellationToken cancellationToken)
    {
        var assignments = await _assignmentRepository
            .GetActiveByRoleAsync(query.RoleId, query.TenantId, cancellationToken);

        if (assignments.Count == 0)
            return Array.Empty<RoleMemberDto>();

        var userIds = assignments.Select(a => a.UserId).Distinct();
        var users = await _identityService
            .GetUsersByIdsAsync(userIds, query.TenantId, cancellationToken);

        var userMap = users.ToDictionary(u => u.Id);

        return assignments
            .Select(a =>
            {
                userMap.TryGetValue(a.UserId, out var user);
                return new RoleMemberDto(
                    a.Id,
                    a.UserId,
                    user?.Email ?? string.Empty,
                    user?.DisplayName ?? string.Empty,
                    a.ScopeId,
                    a.CreatedAt,
                    a.ExpiresAt);
            })
            .OrderBy(m => m.DisplayName)
            .ToList()
            .AsReadOnly();
    }
}
