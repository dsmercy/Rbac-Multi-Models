using BuildingBlocks.Application;
using RbacCore.Application.Common;

namespace RbacCore.Application.Queries;

public sealed record ListPermissionsQuery(Guid TenantId) : IQuery<IReadOnlyList<PermissionDto>>;
