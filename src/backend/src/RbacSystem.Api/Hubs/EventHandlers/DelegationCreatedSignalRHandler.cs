using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" when a delegation is created.
/// ResourceId carries the DelegateeId so the React client can scope its
/// re-evaluation of permission checks to the affected user.
/// </summary>
public sealed class DelegationCreatedSignalRHandler : INotificationHandler<DelegationCreatedEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public DelegationCreatedSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(DelegationCreatedEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("delegation", notification.TenantId, notification.DelegateeId, notification.OccurredAt),
                   cancellationToken);
}
