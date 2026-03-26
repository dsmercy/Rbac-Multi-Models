namespace Delegation.Application.Common;

public sealed record ActiveDelegationDto(
    Guid Id,
    Guid TenantId,
    Guid DelegatorId,
    Guid DelegateeId,
    IReadOnlyList<string> PermissionCodes,
    Guid ScopeId,
    DateTimeOffset ExpiresAt,
    int ChainDepth,
    DateTimeOffset CreatedAt);
