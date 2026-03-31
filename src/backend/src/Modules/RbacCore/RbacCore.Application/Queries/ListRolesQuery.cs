using BuildingBlocks.Application;
using RbacCore.Application.Common;

namespace RbacCore.Application.Queries;

public sealed record ListRolesQuery(Guid TenantId) : IQuery<IReadOnlyList<RoleDto>>;
