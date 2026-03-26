using BuildingBlocks.Domain;

namespace Identity.Domain.Events;

public sealed class UserDeactivatedEvent : DomainEvent
{
    public Guid UserId { get; }
    public string Reason { get; }

    public UserDeactivatedEvent(Guid userId, Guid tenantId, string reason)
    {
        UserId = userId;
        TenantId = tenantId;
        Reason = reason;
    }
}
