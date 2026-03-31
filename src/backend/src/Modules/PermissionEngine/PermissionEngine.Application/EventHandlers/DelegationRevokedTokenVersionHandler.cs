using BuildingBlocks.Domain.Events;
using MediatR;
using PermissionEngine.Domain.Interfaces;

namespace PermissionEngine.Application.EventHandlers;

/// <summary>
/// Invalidates the delegatee's permission cache and increments their token
/// version immediately when a delegation is revoked.
///
/// Security guarantee:
///   Revocation must take effect before the next permission evaluation —
///   not just at next login. By incrementing the token version:
///     1. Any cached permission grants sourced from the delegation are
///        immediately invalid (token-version mismatch on next cache read).
///     2. Any in-flight JWT the delegatee holds will be rejected on the
///        next permission-engine evaluation (TokenVersionValidationStep step 0).
///
///   This means the maximum window for a revoked delegation to remain effective
///   is zero for clients actively making permission checks, and at most the
///   cache TTL (60s by default) for idle clients, which then auto-expires.
///
///   Per Phase 4 spec: "immediately marks the delegation as revoked, busts
///   delegation:{tid}:{uid} cache, increments token-version:{uid} of the
///   delegatee, and emits DelegationRevoked event."
/// </summary>
public sealed class DelegationRevokedTokenVersionHandler
    : INotificationHandler<DelegationRevokedEvent>
{
    private readonly IPermissionCacheService _cache;

    public DelegationRevokedTokenVersionHandler(IPermissionCacheService cache)
        => _cache = cache;

    public Task Handle(
        DelegationRevokedEvent notification,
        CancellationToken cancellationToken)
        // InvalidateUserAsync = increment version + bust cached perm entries
        => _cache.InvalidateUserAsync(
            notification.DelegateeId,
            notification.TenantId,
            cancellationToken);
}
