using BuildingBlocks.Application;

namespace Delegation.Application.Commands;

public sealed record CreateDelegationCommand(
    Guid TenantId,
    Guid DelegatorId,
    Guid DelegateeId,
    IReadOnlyList<string> PermissionCodes,
    Guid ScopeId,
    DateTimeOffset ExpiresAt,
    Guid CreatedByUserId) : ICommand<Guid>;
