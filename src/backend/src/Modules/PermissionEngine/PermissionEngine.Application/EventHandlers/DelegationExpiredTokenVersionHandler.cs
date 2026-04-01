using BuildingBlocks.Domain.Events;
using MediatR;
using PermissionEngine.Application.Telemetry;
using PermissionEngine.Domain.Interfaces;

namespace PermissionEngine.Application.EventHandlers;

/// <summary>
/// Invalidates the delegatee's permission cache when a delegation expires.
///
/// Per CLAUDE.md eviction map:
///   DelegationExpired → bust perm:{tid}:{uid}:*, delegation:{tid}:{uid},
///                        increment token-version:{uid}
///
/// Expiry is detected at evaluation time (no background job), but this handler
/// ensures the cache is cleaned up promptly when expiry is first observed,
/// preventing stale cache entries from being served in the brief window before
/// the token version mismatch is detected.
///
/// Effect mirrors DelegationRevoked: InvalidateUserAsync increments the
/// delegatee's token version and invalidates all cached permission entries.
/// </summary>
public sealed class DelegationExpiredTokenVersionHandler
    : INotificationHandler<DelegationExpiredEvent>
{
    private readonly IPermissionCacheService _cache;

    public DelegationExpiredTokenVersionHandler(IPermissionCacheService cache)
        => _cache = cache;

    public Task Handle(DelegationExpiredEvent notification, CancellationToken cancellationToken)
    {
        RbacMetrics.DelegationEnded(notification.TenantId.ToString());
        RbacMetrics.RecordCacheEviction("delegation",    notification.TenantId.ToString());
        RbacMetrics.RecordCacheEviction("token-version", notification.TenantId.ToString());
        return _cache.InvalidateUserAsync(
            notification.DelegateeId,
            notification.TenantId,
            cancellationToken);
    }
}
