using BuildingBlocks.Application;
using RbacCore.Application.Common;

namespace RbacCore.Application.Queries;

public sealed record GetRoleByIdQuery(Guid TenantId, Guid RoleId) : IQuery<RoleDto?>;
