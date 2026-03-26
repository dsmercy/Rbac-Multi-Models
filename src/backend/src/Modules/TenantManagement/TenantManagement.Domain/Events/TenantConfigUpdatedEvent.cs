using BuildingBlocks.Domain;

namespace TenantManagement.Domain.Events;

public sealed class TenantConfigUpdatedEvent : DomainEvent
{
    public Guid UpdatedByUserId { get; }

    public TenantConfigUpdatedEvent(Guid tenantId, Guid updatedByUserId)
    {
        TenantId = tenantId;
        UpdatedByUserId = updatedByUserId;
    }
}
