using BuildingBlocks.Domain;

namespace Identity.Domain.Events;

public sealed class UserReactivatedEvent : DomainEvent
{
    public Guid UserId { get; }

    public UserReactivatedEvent(Guid userId, Guid tenantId)
    {
        UserId = userId;
        TenantId = tenantId;
    }
}
