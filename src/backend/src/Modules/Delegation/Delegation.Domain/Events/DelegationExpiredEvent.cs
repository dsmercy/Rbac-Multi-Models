using BuildingBlocks.Domain;

namespace Delegation.Domain.Events;

public sealed class DelegationExpiredEvent : DomainEvent
{
    public Guid DelegationId { get; }
    public Guid DelegateeId { get; }

    public DelegationExpiredEvent(Guid delegationId, Guid tenantId, Guid delegateeId)
    {
        DelegationId = delegationId;
        TenantId = tenantId;
        DelegateeId = delegateeId;
    }
}
