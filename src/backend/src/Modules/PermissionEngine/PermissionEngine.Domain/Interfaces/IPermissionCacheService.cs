using PermissionEngine.Domain.Models;

namespace PermissionEngine.Domain.Interfaces;

public interface IPermissionCacheService
{
    Task<AccessResult?> GetAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId,
        CancellationToken ct = default);

    Task SetAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId,
        AccessResult result, TimeSpan ttl,
        CancellationToken ct = default);

    Task InvalidateUserAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Attempts to acquire a SET NX stampede-protection lock for the given permission key.
    /// Returns true if the lock was acquired (caller should compute).
    /// Returns false if another caller already holds it (caller should wait and retry cache).
    /// </summary>
    Task<bool> TryAcquireStampedeLockAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId,
        CancellationToken ct = default);

    /// <summary>Releases the stampede-protection lock acquired by <see cref="TryAcquireStampedeLockAsync"/>.</summary>
    Task ReleaseStampedeLockAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId);

    /// <summary>
    /// Deletes all <c>perm:{tenantId}:*</c> cache entries.
    /// Called on PolicyCreated / PolicyUpdated / PolicyDeleted events.
    /// </summary>
    Task InvalidateTenantPermCacheAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Deletes all cache keys for the tenant (perm, roles, policy, delegation, scope-tree).
    /// Called on TenantSuspended event — maximum blast radius is intentional.
    /// </summary>
    Task InvalidateAllTenantKeysAsync(Guid tenantId, CancellationToken ct = default);

    // ── roles:{tenantId}:{userId}  TTL 300s ────────────────────────────────────

    /// <summary>
    /// Returns the cached list of effective permission codes for the user across all scopes.
    /// Used by ScopeInheritanceStep to avoid repeated DB walks on every eval.
    /// </summary>
    Task<IReadOnlyList<string>?> GetUserPermissionCodesAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default);

    Task SetUserPermissionCodesAsync(
        Guid userId, Guid tenantId, IReadOnlyList<string> codes, CancellationToken ct = default);

    // ── scope-tree:{tenantId}:{scopeId}  TTL 3600s ────────────────────────────

    /// <summary>
    /// Returns the cached ancestor scope IDs for a given scope.
    /// Used by ScopeInheritanceStep to avoid repeated closure-table traversals.
    /// </summary>
    Task<IReadOnlyList<Guid>?> GetScopeAncestorsAsync(
        Guid scopeId, Guid tenantId, CancellationToken ct = default);

    Task SetScopeAncestorsAsync(
        Guid scopeId, Guid tenantId, IReadOnlyList<Guid> ancestorIds, CancellationToken ct = default);

    // ── delegation:{tenantId}:{userId}  TTL 60s ────────────────────────────────

    /// <summary>
    /// Returns the cached active delegation for a user (JSON-serialized).
    /// Null = no active delegation in cache (cache miss, not "no delegation").
    /// </summary>
    Task<string?> GetDelegationJsonAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default);

    Task SetDelegationJsonAsync(
        Guid userId, Guid tenantId, string delegationJson, CancellationToken ct = default);

    // ── token version ─────────────────────────────────────────────────────────

    Task<int> GetTokenVersionAsync(Guid userId, CancellationToken ct = default);

    Task IncrementTokenVersionAsync(Guid userId, CancellationToken ct = default);
}
