using BuildingBlocks.Application;
using RbacCore.Application.Common;

namespace RbacCore.Application.Queries;

public sealed record GetEffectivePermissionsQuery(
    Guid UserId,
    Guid TenantId,
    Guid? ScopeId) : IQuery<IReadOnlyList<PermissionDto>>;
