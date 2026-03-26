using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace PermissionEngine.Infrastructure.Cache;

public sealed class RedisPermissionCacheService : IPermissionCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public RedisPermissionCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = redis.GetDatabase();
    }

    // Key format: perm:{tenantId}:{userId}:{action}:{resourceId}:{scopeId}:{tokenVersion}
    private static string BuildKey(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId)
        => $"perm:{tenantId}:{userId}:{action}:{resourceId}:{scopeId}";

    private static string TokenVersionKey(Guid userId)
        => $"token-version:{userId}";

    public async Task<AccessResult?> GetAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId,
        CancellationToken ct = default)
    {
        var key = BuildKey(userId, action, resourceId, scopeId, tenantId);
        var value = await _db.StringGetAsync(key);

        if (!value.HasValue)
            return null;

        var cached = JsonSerializer.Deserialize<CachedAccessResult>(value.ToString());
        if (cached is null) return null;

        // Validate token version — if user's token version has changed, bust cache
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
        var key = BuildKey(userId, action, resourceId, scopeId, tenantId);
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
        // Increment token version — all cached entries for this user become stale
        await IncrementTokenVersionAsync(userId, ct);
    }

    public async Task<int> GetTokenVersionAsync(
        Guid userId, CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(TokenVersionKey(userId));
        return val.HasValue ? (int)val : 0;
    }

    public Task IncrementTokenVersionAsync(
        Guid userId, CancellationToken ct = default)
        => _db.StringIncrementAsync(TokenVersionKey(userId));

    private sealed record CachedAccessResult(
        bool IsGranted,
        int? DenialReason,
        int TokenVersion);
}
