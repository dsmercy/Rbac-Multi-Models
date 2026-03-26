using BuildingBlocks.Application;
using RbacCore.Application.Common;

namespace RbacCore.Application.Commands;

public sealed record CreateRoleCommand(
    Guid TenantId,
    string Name,
    string? Description,
    Guid CreatedByUserId,
    bool IsSystemRole = false) : ICommand<RoleDto>;
