using BuildingBlocks.Application;
using RbacCore.Application.Common;

namespace RbacCore.Application.Queries;

public sealed record ListRoleMembersQuery(Guid TenantId, Guid RoleId) : IQuery<IReadOnlyList<RoleMemberDto>>;
