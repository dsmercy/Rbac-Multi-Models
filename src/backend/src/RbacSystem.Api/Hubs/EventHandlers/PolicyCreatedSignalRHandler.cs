using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" to all tenant clients when a new policy is created.
/// React client refetches the policies list and re-evaluates cached permission checks
/// because new policies can change access decisions.
/// </summary>
public sealed class PolicyCreatedSignalRHandler : INotificationHandler<PolicyCreatedEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public PolicyCreatedSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(PolicyCreatedEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("policy", notification.TenantId, notification.PolicyId, notification.OccurredAt),
                   cancellationToken);
}
