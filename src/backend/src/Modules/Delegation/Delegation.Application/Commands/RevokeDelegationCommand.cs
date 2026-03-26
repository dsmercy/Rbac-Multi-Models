using BuildingBlocks.Application;

namespace Delegation.Application.Commands;

public sealed record RevokeDelegationCommand(
    Guid TenantId,
    Guid DelegationId,
    Guid RevokedByUserId) : ICommand;
