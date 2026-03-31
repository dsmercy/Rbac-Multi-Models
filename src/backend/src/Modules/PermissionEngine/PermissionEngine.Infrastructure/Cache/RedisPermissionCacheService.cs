using BuildingBlocks.Application;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace PermissionEngine.Infrastructure.Cache;

/// <summary>
/// Redis-backed implementation of both IPermissionCacheService and ITokenVersionService.
///
/// Key schemas (see Phase 5 spec):
///   perm:{tenantId}:{userId}:{action}:{resourceId}:{scopeId}  → CachedAccessResult  TTL: tenant-configured
///   token-version:{userId}                                     → int                 TTL: sliding 7 days
///
/// Token version design:
///   • Stored as a plain Redis integer — INCR is atomic.
///   • TTL is sliding 7 days (reset on every read, approximated by re-setting EXPIRE).
///   • A missing key (new user, never incremented) is treated as version 0.
///   • Increment is fired by event handlers on: UserRoleAssigned, UserRoleRevoked,
///     DelegationCreated, DelegationRevoked.
///
/// Cache invalidation:
///   • InvalidateUserAsync bumps the token version, causing all cached perm entries
///     for this user to be rejected on next read (token-version mismatch).
///   • This is more efficient than wildcard-deleting perm:* keys (Redis does not
///     support SCAN + DEL as an atomic operation and it's O(N)).
/// </summary>
public sealed class RedisPermissionCacheService : IPermissionCacheService, ITokenVersionService
{
    private readonly IDatabase _db;

    /// <summary>Sliding TTL for token-version keys: must outlive the refresh-token lifetime.</summary>
    private static readonly TimeSpan TokenVersionTtl = TimeSpan.FromDays(7);

    public RedisPermissionCacheService(IConnectionMultiplexer redis)
        => _db = redis.GetDatabase();

    // ── Key builders ──────────────────────────────────────────────────────────

    private static string PermKey(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId)
        => $"perm:{tenantId}:{userId}:{action}:{resourceId}:{scopeId}";

    private static string TokenVersionKey(Guid userId)
        => $"token-version:{userId}";

    // ── IPermissionCacheService ───────────────────────────────────────────────

    public async Task<AccessResult?> GetAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId,
        CancellationToken ct = default)
    {
        var key = PermKey(userId, action, resourceId, scopeId, tenantId);
        var value = await _db.StringGetAsync(key);

        if (!value.HasValue)
            return null;

        var cached = JsonSerializer.Deserialize<CachedAccessResult>(value.ToString());
        if (cached is null) return null;

        // Reject cache entry if the user's token version has been incremented
        // since this entry was written (role or delegation change occurred).
        var currentVersion = await GetTokenVersionAsync(userId, ct);
        if (cached.TokenVersion != currentVersion)
        {
            await _db.KeyDeleteAsync(key);
            return null;
        }

        return cached.IsGranted
            ? AccessResult.Granted(cacheHit: true, latencyMs: 0)
            : AccessResult.DeniedFromCache((DenialReason)cached.DenialReason!);
    }

    public async Task SetAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId,
        AccessResult result, TimeSpan ttl,
        CancellationToken ct = default)
    {
        var key = PermKey(userId, action, resourceId, scopeId, tenantId);
        var version = await GetTokenVersionAsync(userId, ct);

        var cached = new CachedAccessResult(
            result.IsGranted,
            result.Reason.HasValue ? (int)result.Reason.Value : (int?)null,
            version);

        var json = JsonSerializer.Serialize(cached);
        await _db.StringSetAsync(key, json, ttl);
    }

    public async Task InvalidateUserAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        // Incrementing the token version invalidates all cached permission entries
        // for this user — they carry the old version and will be rejected on read.
        await IncrementTokenVersionAsync(userId, ct);
    }

    // ── ITokenVersionService (also used by IPermissionCacheService internally) ─

    /// <inheritdoc />
    public async Task<int> GetTokenVersionAsync(
        Guid userId, CancellationToken ct = default)
    {
        var key = TokenVersionKey(userId);
        var val = await _db.StringGetAsync(key);

        if (!val.HasValue)
            return 0; // No key = version 0 (new user, never incremented)

        // Refresh the sliding TTL on every read
        await _db.KeyExpireAsync(key, TokenVersionTtl);

        return (int)val;
    }

    /// <inheritdoc />
    public async Task IncrementTokenVersionAsync(
        Guid userId, CancellationToken ct = default)
    {
        var key = TokenVersionKey(userId);

        // INCR is atomic — safe under concurrent role-change events
        await _db.StringIncrementAsync(key);

        // Reset sliding TTL after increment
        await _db.KeyExpireAsync(key, TokenVersionTtl);
    }

    // ── Private types ─────────────────────────────────────────────────────────

    private sealed record CachedAccessResult(
        bool IsGranted,
        int? DenialReason,
        int TokenVersion);
}
