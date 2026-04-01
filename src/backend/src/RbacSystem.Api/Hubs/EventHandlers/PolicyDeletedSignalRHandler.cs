using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" to all tenant clients when a policy is deleted.
/// React client refetches policies and re-evaluates cached permission checks
/// because removing a policy can open or close access for users.
/// </summary>
public sealed class PolicyDeletedSignalRHandler : INotificationHandler<PolicyDeletedEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public PolicyDeletedSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(PolicyDeletedEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("policy", notification.TenantId, notification.PolicyId, notification.OccurredAt),
                   cancellationToken);
}
