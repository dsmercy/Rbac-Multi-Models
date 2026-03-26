using BuildingBlocks.Domain;

namespace TenantManagement.Domain.Events;

public sealed class TenantReactivatedEvent : DomainEvent
{
    public Guid ReactivatedByUserId { get; }

    public TenantReactivatedEvent(Guid tenantId, Guid reactivatedByUserId)
    {
        TenantId = tenantId;
        ReactivatedByUserId = reactivatedByUserId;
    }
}
