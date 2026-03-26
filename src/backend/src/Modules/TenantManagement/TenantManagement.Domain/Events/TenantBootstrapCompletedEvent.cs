using BuildingBlocks.Domain;

namespace TenantManagement.Domain.Events;

public sealed class TenantBootstrapCompletedEvent : DomainEvent
{
    public Guid AdminUserId { get; }

    public TenantBootstrapCompletedEvent(Guid tenantId, Guid adminUserId)
    {
        TenantId = tenantId;
        AdminUserId = adminUserId;
    }
}
