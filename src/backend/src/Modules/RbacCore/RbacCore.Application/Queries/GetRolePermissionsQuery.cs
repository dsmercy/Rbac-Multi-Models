using BuildingBlocks.Application;
using RbacCore.Application.Common;

namespace RbacCore.Application.Queries;

public sealed record GetRolePermissionsQuery(Guid TenantId, Guid RoleId) : IQuery<IReadOnlyList<PermissionDto>>;
