using MediatR;

namespace BuildingBlocks.Domain;

/// <summary>
/// All domain events implement INotification so MediatR can publish them
/// in-process. When extracting to microservices, replace IPublisher with
/// an outbox/message-broker dispatcher — the event contracts stay unchanged.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    Guid TenantId { get; }
    DateTimeOffset OccurredAt { get; }
}
