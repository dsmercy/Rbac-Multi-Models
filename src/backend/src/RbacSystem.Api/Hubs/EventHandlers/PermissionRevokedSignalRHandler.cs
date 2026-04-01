using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" when a permission is revoked from a role.
/// React client refetches the permission matrix and re-evaluates cached checks.
/// </summary>
public sealed class PermissionRevokedSignalRHandler : INotificationHandler<PermissionRevokedEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public PermissionRevokedSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(PermissionRevokedEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("permission", notification.TenantId, notification.RoleId, notification.OccurredAt),
                   cancellationToken);
}
