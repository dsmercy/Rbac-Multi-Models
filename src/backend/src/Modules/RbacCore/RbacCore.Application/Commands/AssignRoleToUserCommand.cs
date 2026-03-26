using BuildingBlocks.Application;

namespace RbacCore.Application.Commands;

public sealed record AssignRoleToUserCommand(
    Guid TenantId,
    Guid UserId,
    Guid RoleId,
    Guid? ScopeId,
    DateTimeOffset? ExpiresAt,
    Guid AssignedByUserId) : ICommand;
