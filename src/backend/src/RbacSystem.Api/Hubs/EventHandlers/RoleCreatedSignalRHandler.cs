using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" to all tenant clients when a new role is created.
/// React client refetches the roles list.
/// </summary>
public sealed class RoleCreatedSignalRHandler : INotificationHandler<RoleCreatedEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public RoleCreatedSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(RoleCreatedEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("role", notification.TenantId, notification.RoleId, notification.OccurredAt),
                   cancellationToken);
}
