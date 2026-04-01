using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" when a role is soft-deleted.
/// React client must refetch roles list AND re-evaluate all cached permission checks
/// because any user who held that role is now effectively demoted.
/// </summary>
public sealed class RoleDeletedSignalRHandler : INotificationHandler<RoleDeletedEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public RoleDeletedSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(RoleDeletedEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("role", notification.TenantId, notification.RoleId, notification.OccurredAt),
                   cancellationToken);
}
