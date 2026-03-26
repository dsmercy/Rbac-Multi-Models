using BuildingBlocks.Application;

namespace RbacCore.Application.Commands;

public sealed record DeleteRoleCommand(
    Guid TenantId,
    Guid RoleId,
    Guid DeletedByUserId) : ICommand;
