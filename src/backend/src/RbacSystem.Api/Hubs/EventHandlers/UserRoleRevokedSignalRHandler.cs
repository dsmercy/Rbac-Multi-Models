using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" when a role is revoked from a user.
/// React client must re-evaluate cached permission checks for the affected user
/// and refresh the user-role assignment list.
/// </summary>
public sealed class UserRoleRevokedSignalRHandler : INotificationHandler<UserRoleRevokedEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public UserRoleRevokedSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(UserRoleRevokedEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("assignment", notification.TenantId, notification.UserId, notification.OccurredAt, notification.RoleId),
                   cancellationToken);
}
