using BuildingBlocks.Domain;

namespace Identity.Domain.Events;

public sealed class UserCreatedEvent : DomainEvent
{
    public Guid UserId { get; }
    public string Email { get; }
    public string DisplayName { get; }

    public UserCreatedEvent(
        Guid userId,
        Guid tenantId,
        string email,
        string displayName)
    {
        UserId = userId;
        TenantId = tenantId;
        Email = email;
        DisplayName = displayName;
    }
}
