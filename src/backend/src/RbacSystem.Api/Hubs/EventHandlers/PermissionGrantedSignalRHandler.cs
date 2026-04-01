using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" when a permission is granted to a role.
/// React client refetches the permission matrix for the affected role.
/// </summary>
public sealed class PermissionGrantedSignalRHandler : INotificationHandler<PermissionGrantedEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public PermissionGrantedSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(PermissionGrantedEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("permission", notification.TenantId, notification.RoleId, notification.OccurredAt),
                   cancellationToken);
}
