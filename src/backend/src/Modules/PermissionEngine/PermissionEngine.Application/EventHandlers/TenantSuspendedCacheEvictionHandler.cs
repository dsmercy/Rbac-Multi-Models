using BuildingBlocks.Domain.Events;
using MediatR;
using PermissionEngine.Domain.Interfaces;

namespace PermissionEngine.Application.EventHandlers;

/// <summary>
/// Flushes ALL cache keys for the tenant when it is suspended.
///
/// Per CLAUDE.md eviction map:
///   TenantSuspended → all keys matching *:{tid}:*
///
/// This is the maximum blast radius invalidation. Suspended tenants must not
/// serve cached grants — every subsequent request re-evaluates from the DB
/// and hits the TenantSuspended guard before any pipeline step runs.
/// </summary>
public sealed class TenantSuspendedCacheEvictionHandler
    : INotificationHandler<TenantSuspendedEvent>
{
    private readonly IPermissionCacheService _cache;

    public TenantSuspendedCacheEvictionHandler(IPermissionCacheService cache)
        => _cache = cache;

    public Task Handle(TenantSuspendedEvent notification, CancellationToken cancellationToken)
        => _cache.InvalidateAllTenantKeysAsync(notification.TenantId, cancellationToken);
}
