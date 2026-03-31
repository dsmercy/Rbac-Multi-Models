using Delegation.Domain.Events;
using MediatR;
using PermissionEngine.Domain.Interfaces;

namespace PermissionEngine.Application.EventHandlers;

/// <summary>
/// Increments the delegatee's token version when a new delegation is created.
///
/// Effect:
///   The delegatee's current JWT does not contain the newly delegated
///   permissions. By incrementing the token version, the next permission-engine
///   evaluation with the old JWT will reject it (tv mismatch → 401), causing
///   the client to refresh and get a new JWT that reflects the delegation.
///
/// We do NOT invalidate the delegator's token version — the delegator's
/// permissions have not changed, only who can act on their behalf.
/// </summary>
public sealed class DelegationCreatedTokenVersionHandler
    : INotificationHandler<DelegationCreatedEvent>
{
    private readonly IPermissionCacheService _cache;

    public DelegationCreatedTokenVersionHandler(IPermissionCacheService cache)
        => _cache = cache;

    public Task Handle(
        DelegationCreatedEvent notification,
        CancellationToken cancellationToken)
        => _cache.IncrementTokenVersionAsync(notification.DelegateeId, cancellationToken);
}
