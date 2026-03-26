using BuildingBlocks.Domain;

namespace Identity.Domain.Events;

public sealed class UserPasswordResetEvent : DomainEvent
{
    public Guid UserId { get; }

    public UserPasswordResetEvent(Guid userId, Guid tenantId)
    {
        UserId = userId;
        TenantId = tenantId;
    }
}
