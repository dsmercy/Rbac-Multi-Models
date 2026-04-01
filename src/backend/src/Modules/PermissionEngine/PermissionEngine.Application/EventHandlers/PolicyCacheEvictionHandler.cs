using BuildingBlocks.Domain.Events;
using MediatR;
using PermissionEngine.Application.Telemetry;
using PermissionEngine.Domain.Interfaces;

namespace PermissionEngine.Application.EventHandlers;

/// <summary>
/// Busts all <c>perm:{tenantId}:*</c> cache entries when a policy is
/// created, updated, or deleted.
///
/// Rationale: policies affect the outcome of every permission evaluation in
/// a tenant. Any cached result may now be incorrect regardless of which user
/// or resource it is for. We flush the entire tenant perm cache and let the
/// next request recompute from the database.
///
/// Per CLAUDE.md eviction map:
///   PolicyCreated / PolicyUpdated / PolicyDeleted
///     → bust perm:{tid}:* and policy:{tid}:{policyId}
/// </summary>
public sealed class PolicyCacheEvictionHandler
    : INotificationHandler<PolicyCreatedEvent>,
      INotificationHandler<PolicyUpdatedEvent>,
      INotificationHandler<PolicyDeletedEvent>
{
    private readonly IPermissionCacheService _cache;

    public PolicyCacheEvictionHandler(IPermissionCacheService cache)
        => _cache = cache;

    public Task Handle(PolicyCreatedEvent notification, CancellationToken cancellationToken)
    {
        RbacMetrics.RecordCacheEviction("perm",   notification.TenantId.ToString());
        RbacMetrics.RecordCacheEviction("policy", notification.TenantId.ToString());
        return _cache.InvalidateTenantPermCacheAsync(notification.TenantId, cancellationToken);
    }

    public Task Handle(PolicyUpdatedEvent notification, CancellationToken cancellationToken)
    {
        RbacMetrics.RecordCacheEviction("perm",   notification.TenantId.ToString());
        RbacMetrics.RecordCacheEviction("policy", notification.TenantId.ToString());
        return _cache.InvalidateTenantPermCacheAsync(notification.TenantId, cancellationToken);
    }

    public Task Handle(PolicyDeletedEvent notification, CancellationToken cancellationToken)
    {
        RbacMetrics.RecordCacheEviction("perm",   notification.TenantId.ToString());
        RbacMetrics.RecordCacheEviction("policy", notification.TenantId.ToString());
        return _cache.InvalidateTenantPermCacheAsync(notification.TenantId, cancellationToken);
    }
}
