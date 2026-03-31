using BuildingBlocks.Application;
using RbacCore.Application.Common;

namespace RbacCore.Application.Commands;

public sealed record UpdateRoleCommand(
    Guid TenantId,
    Guid RoleId,
    string Name,
    string? Description,
    Guid UpdatedByUserId) : ICommand<RoleDto>;
