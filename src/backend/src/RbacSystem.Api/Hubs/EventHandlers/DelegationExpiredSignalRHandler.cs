using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" when an expired delegation is detected at evaluation time.
/// ResourceId carries the DelegateeId so the React client can scope its
/// re-evaluation of permission checks to the affected user.
/// </summary>
public sealed class DelegationExpiredSignalRHandler : INotificationHandler<DelegationExpiredEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public DelegationExpiredSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(DelegationExpiredEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("delegation", notification.TenantId, notification.DelegateeId, notification.OccurredAt),
                   cancellationToken);
}
