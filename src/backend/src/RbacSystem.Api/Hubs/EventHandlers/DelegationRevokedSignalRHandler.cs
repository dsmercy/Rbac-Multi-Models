using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" when a delegation is revoked.
/// ResourceId carries the DelegateeId so the React client can scope its
/// re-evaluation of permission checks to the affected user.
/// </summary>
public sealed class DelegationRevokedSignalRHandler : INotificationHandler<DelegationRevokedEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public DelegationRevokedSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(DelegationRevokedEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("delegation", notification.TenantId, notification.DelegateeId, notification.OccurredAt),
                   cancellationToken);
}
