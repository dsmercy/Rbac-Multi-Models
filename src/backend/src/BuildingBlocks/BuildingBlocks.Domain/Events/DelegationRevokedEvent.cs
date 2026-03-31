namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by Delegation module when a delegation is revoked.
/// Consumed cross-module by: PermissionEngine (token version), AuditLogging.
/// </summary>
public sealed class DelegationRevokedEvent : DomainEvent
{
    public Guid DelegationId    { get; }
    public Guid DelegateeId     { get; }
    public Guid RevokedByUserId { get; }

    public DelegationRevokedEvent(
        Guid delegationId, Guid tenantId, Guid delegateeId, Guid revokedByUserId)
    {
        DelegationId    = delegationId;
        TenantId        = tenantId;
        DelegateeId     = delegateeId;
        RevokedByUserId = revokedByUserId;
    }
}
