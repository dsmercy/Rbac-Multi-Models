using MediatR;
using PermissionEngine.Domain.Interfaces;
using BuildingBlocks.Domain.Events;

namespace PermissionEngine.Application.EventHandlers;

/// <summary>
/// Increments the token version in Redis when a role is assigned to a user.
///
/// Effect:
///   Any JWT the user currently holds will be considered stale on the next
///   permission-engine evaluation. The user will receive a 401 and must
///   re-authenticate (or the client silently refreshes via the refresh token)
///   to get a new JWT that embeds the updated role set.
///
/// This is the correct revocation behaviour: we do NOT force a logout, we
/// force a token refresh. The refresh-token path re-embeds the new roles.
/// </summary>
public sealed class UserRoleAssignedTokenVersionHandler
    : INotificationHandler<UserRoleAssignedEvent>
{
    private readonly IPermissionCacheService _cache;

    public UserRoleAssignedTokenVersionHandler(IPermissionCacheService cache)
        => _cache = cache;

    public Task Handle(
        UserRoleAssignedEvent notification,
        CancellationToken cancellationToken)
        => _cache.IncrementTokenVersionAsync(notification.UserId, cancellationToken);
}
