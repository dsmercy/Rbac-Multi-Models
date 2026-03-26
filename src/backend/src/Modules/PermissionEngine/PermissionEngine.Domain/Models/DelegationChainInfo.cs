namespace PermissionEngine.Domain.Models;

public sealed record DelegationChainInfo(
    Guid DelegationId,
    Guid DelegatorId,
    Guid DelegateeId,
    int ChainDepth);
