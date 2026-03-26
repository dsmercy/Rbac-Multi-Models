using BuildingBlocks.Domain;

namespace Identity.Domain.Events;

public sealed class UserEmailChangedEvent : DomainEvent
{
    public Guid UserId { get; }
    public string OldEmail { get; }
    public string NewEmail { get; }

    public UserEmailChangedEvent(
        Guid userId,
        Guid tenantId,
        string oldEmail,
        string newEmail)
    {
        UserId = userId;
        TenantId = tenantId;
        OldEmail = oldEmail;
        NewEmail = newEmail;
    }
}
