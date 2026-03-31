using MediatR;
using PermissionEngine.Domain.Interfaces;
using BuildingBlocks.Domain.Events;

namespace PermissionEngine.Application.EventHandlers;

/// <summary>
/// Invalidates the permission cache and increments the token version when a
/// role is revoked from a user.
///
/// Security note:
///   Role revocation is the highest-priority cache invalidation event.
///   We call InvalidateUserAsync (which itself calls IncrementTokenVersionAsync)
///   to ensure:
///     1. Any cached permission grants are rejected on next read.
///     2. Any in-flight JWT is forced through re-authentication on next
///        permission evaluation.
///
///   Maximum revocation latency = access-token TTL (15 minutes) unless the
///   user makes a permission-engine call before expiry, at which point the
///   stale token version causes immediate 401.
/// </summary>
public sealed class UserRoleRevokedTokenVersionHandler
    : INotificationHandler<UserRoleRevokedEvent>
{
    private readonly IPermissionCacheService _cache;

    public UserRoleRevokedTokenVersionHandler(IPermissionCacheService cache)
        => _cache = cache;

    public Task Handle(
        UserRoleRevokedEvent notification,
        CancellationToken cancellationToken)
        // InvalidateUserAsync increments version AND busts perm cache entries
        => _cache.InvalidateUserAsync(
            notification.UserId,
            notification.TenantId,
            cancellationToken);
}
