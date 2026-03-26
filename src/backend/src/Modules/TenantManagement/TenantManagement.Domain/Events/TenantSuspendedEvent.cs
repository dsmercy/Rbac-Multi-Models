using BuildingBlocks.Domain;

namespace TenantManagement.Domain.Events;

public sealed class TenantSuspendedEvent : DomainEvent
{
    public string Reason { get; }
    public Guid SuspendedByUserId { get; }

    public TenantSuspendedEvent(Guid tenantId, string reason, Guid suspendedByUserId)
    {
        TenantId = tenantId;
        Reason = reason;
        SuspendedByUserId = suspendedByUserId;
    }
}
