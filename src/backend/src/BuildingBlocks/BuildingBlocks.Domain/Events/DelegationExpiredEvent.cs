namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by Delegation module when a delegation's expiry is detected at evaluation time.
/// Consumed cross-module by: PermissionEngine (cache eviction).
/// </summary>
public sealed class DelegationExpiredEvent : DomainEvent
{
    public Guid DelegationId { get; }
    public Guid DelegatorId  { get; }
    public Guid DelegateeId  { get; }

    public DelegationExpiredEvent(Guid delegationId, Guid tenantId, Guid delegatorId, Guid delegateeId)
    {
        DelegationId = delegationId;
        TenantId     = tenantId;
        DelegatorId  = delegatorId;
        DelegateeId  = delegateeId;
    }
}
