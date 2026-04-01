using BuildingBlocks.Domain.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs.EventHandlers;

/// <summary>
/// Pushes "rbac:invalidated" to all tenant clients when a policy is updated.
/// React client re-evaluates all cached permission checks for this tenant because
/// an updated policy may change access decisions for multiple users.
/// </summary>
public sealed class PolicyUpdatedSignalRHandler : INotificationHandler<PolicyUpdatedEvent>
{
    private readonly IHubContext<RbacHub> _hub;

    public PolicyUpdatedSignalRHandler(IHubContext<RbacHub> hub) => _hub = hub;

    public Task Handle(PolicyUpdatedEvent notification, CancellationToken cancellationToken)
        => _hub.Clients
               .Group($"tenant:{notification.TenantId}")
               .SendAsync(
                   "rbac:invalidated",
                   new RbacInvalidatedMessage("policy", notification.TenantId, notification.PolicyId, notification.OccurredAt),
                   cancellationToken);
}
