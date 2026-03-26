using BuildingBlocks.Application;

namespace RbacCore.Application.Commands;

public sealed record RevokeRoleFromUserCommand(
    Guid TenantId,
    Guid UserId,
    Guid RoleId,
    Guid? ScopeId,
    Guid RevokedByUserId) : ICommand;
