using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" when a role is assigned to a user.
/// React client re-evaluates the AbilityContext permission checks for that user.
/// ResourceId carries the affected UserId so the client can scope its refetch.
/// </summary>
public sealed class UserRoleAssignedSignalRHandler : INotificationHandler<UserRoleAssignedEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public UserRoleAssignedSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(UserRoleAssignedEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("assignment", notification.TenantId, notification.UserId, notification.OccurredAt, notification.RoleId),
                   cancellationToken);
}
