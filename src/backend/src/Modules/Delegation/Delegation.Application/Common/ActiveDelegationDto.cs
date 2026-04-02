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
    DateTimeOffset CreatedAt,
    bool IsRevoked = false,
    DateTimeOffset? RevokedAt = null)
{
    public string Status =>
        IsRevoked ? "Revoked" :
        ExpiresAt <= DateTimeOffset.UtcNow ? "Expired" :
        "Active";
}
