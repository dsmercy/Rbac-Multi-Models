namespace BuildingBlocks.Domain;

public abstract class DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public Guid TenantId { get; protected init; }
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
