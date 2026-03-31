using BuildingBlocks.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermissionEngine.Domain.Exceptions;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace PermissionEngine.Infrastructure.Cache;

/// <summary>
/// Redis-backed implementation of IPermissionCacheService and ITokenVersionService.
///
/// Cache key schemas (CLAUDE.md Phase 5 spec):
///   perm:{tenantId}:{userId}:{action}:{resourceId}:{scopeId}   TTL: tenant-configured (default 60s)
///   token-version:{userId}                                       TTL: sliding 7 days
///
/// Phase 5 additions:
///   • InvalidateTenantPermCacheAsync  — SCAN + DEL perm:{tenantId}:* (policy change eviction)
///   • InvalidateAllTenantKeysAsync    — SCAN + DEL *:{tenantId}:*    (tenant suspended eviction)
///   • Stampede protection              — SET NX lock before cache miss pipeline
///   • Redis failure mode               — configurable deny-all vs allow-through
///   • Distributed invalidation publish — publishes to cache-invalidation:{tenantId} pub/sub
///
/// Token version design:
///   • Stored as a plain Redis integer — INCR is atomic.
///   • TTL is sliding 7 days (reset on every read).
///   • A missing key is treated as version 0 (new user, never incremented).
/// </summary>
public sealed class RedisPermissionCacheService : IPermissionCacheService, ITokenVersionService
{
    private readonly IConnectionMultiplexer _mux;
    private readonly IDatabase _db;
    private readonly PermissionCacheOptions _options;
    private readonly ILogger<RedisPermissionCacheService> _logger;
    private readonly L1PermissionCache _l1;

    private static readonly TimeSpan TokenVersionTtl = TimeSpan.FromDays(7);

    public RedisPermissionCacheService(
        IConnectionMultiplexer redis,
        IOptions<PermissionCacheOptions> options,
        ILogger<RedisPermissionCacheService> logger,
        L1PermissionCache l1)
    {
        _mux     = redis;
        _db      = redis.GetDatabase();
        _options = options.Value;
        _logger  = logger;
        _l1      = l1;
    }

    // ── Key builders ──────────────────────────────────────────────────────────

    private static string PermKey(Guid tenantId, Guid userId, string action, Guid resourceId, Guid scopeId)
        => $"perm:{tenantId}:{userId}:{action}:{resourceId}:{scopeId}";

    private static string LockKey(Guid tenantId, Guid userId, string action, Guid resourceId, Guid scopeId)
        => $"lock:perm:{tenantId}:{userId}:{action}:{resourceId}:{scopeId}";

    private static string TokenVersionKey(Guid userId)
        => $"token-version:{userId}";

    private static string InvalidationChannel(Guid tenantId)
        => $"cache-invalidation:{tenantId}";

    // ── IPermissionCacheService: permission cache ─────────────────────────────

    public async Task<AccessResult?> GetAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId,
        CancellationToken ct = default)
    {
        try
        {
            var key = PermKey(tenantId, userId, action, resourceId, scopeId);

            // ── L1 (in-process) check ─────────────────────────────────────────
            if (_l1.TryGet(key, out var l1Result) && l1Result is not null)
                return l1Result;

            // ── L2 (Redis) check ──────────────────────────────────────────────
            var value = await _db.StringGetAsync(key);

            if (!value.HasValue)
                return null;

            var cached = JsonSerializer.Deserialize<CachedAccessResult>(value.ToString());
            if (cached is null) return null;

            // Reject stale cache entry: token version incremented since write
            var currentVersion = await GetTokenVersionAsync(userId, ct);
            if (cached.TokenVersion != currentVersion)
            {
                await _db.KeyDeleteAsync(key);
                return null;
            }

            var result = cached.IsGranted
                ? AccessResult.Granted(cacheHit: true, latencyMs: 0)
                : AccessResult.DeniedFromCache((DenialReason)cached.DenialReason!);

            // Backfill L1 for subsequent requests within the same window
            _l1.Set(key, tenantId, result, TimeSpan.FromSeconds(_options.L1CacheTtlSeconds));

            return result;
        }
        catch (RedisException ex)
        {
            return HandleRedisFailure<AccessResult?>(ex, null);
        }
    }

    public async Task SetAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId,
        AccessResult result, TimeSpan ttl,
        CancellationToken ct = default)
    {
        try
        {
            var key     = PermKey(tenantId, userId, action, resourceId, scopeId);
            var version = await GetTokenVersionAsync(userId, ct);

            var cached = new CachedAccessResult(
                result.IsGranted,
                result.Reason.HasValue ? (int)result.Reason.Value : (int?)null,
                version);

            // ── L2 (Redis) write ──────────────────────────────────────────────
            await _db.StringSetAsync(key, JsonSerializer.Serialize(cached), ttl);

            // ── L1 (in-process) write ─────────────────────────────────────────
            _l1.Set(key, tenantId, result, TimeSpan.FromSeconds(_options.L1CacheTtlSeconds));
        }
        catch (RedisException ex)
        {
            HandleRedisFailure<bool>(ex, false);
        }
    }

    // ── Stampede protection ───────────────────────────────────────────────────

    /// <summary>
    /// Attempts to acquire a SET NX lock for a given permission key.
    /// Returns true if this caller acquired the lock (should compute).
    /// Returns false if another caller is already computing (should wait + retry).
    /// The lock expires automatically after <see cref="PermissionCacheOptions.StampedeLockMs"/>
    /// to handle crashes without orphaned locks.
    /// </summary>
    public async Task<bool> TryAcquireStampedeLockAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId,
        CancellationToken ct = default)
    {
        try
        {
            var lockKey = LockKey(tenantId, userId, action, resourceId, scopeId);
            return await _db.StringSetAsync(
                lockKey, "1",
                TimeSpan.FromMilliseconds(_options.StampedeLockMs),
                When.NotExists);
        }
        catch (RedisException)
        {
            return true; // On Redis failure, allow caller to proceed
        }
    }

    public async Task ReleaseStampedeLockAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId)
    {
        try
        {
            var lockKey = LockKey(tenantId, userId, action, resourceId, scopeId);
            await _db.KeyDeleteAsync(lockKey);
        }
        catch (RedisException) { /* Best-effort: lock will expire automatically */ }
    }

    // ── IPermissionCacheService: invalidation ─────────────────────────────────

    public async Task InvalidateUserAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        // Incrementing the token version invalidates all cached perm entries
        // for this user — they carry the old version and will be rejected on read.
        await IncrementTokenVersionAsync(userId, ct);
    }

    /// <inheritdoc />
    public async Task InvalidateTenantPermCacheAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            await ScanAndDeleteAsync($"perm:{tenantId}:*");
            await PublishInvalidationAsync(tenantId);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex,
                "Redis error during perm cache invalidation for tenant {TenantId}", tenantId);
        }
    }

    /// <inheritdoc />
    public async Task InvalidateAllTenantKeysAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            // Bust all key types: perm, roles, policy, delegation, scope-tree, token-version
            // (token-version keys include userId only, not tenantId — handled by pattern below)
            await ScanAndDeleteAsync($"perm:{tenantId}:*");
            await ScanAndDeleteAsync($"roles:{tenantId}:*");
            await ScanAndDeleteAsync($"policy:{tenantId}:*");
            await ScanAndDeleteAsync($"delegation:{tenantId}:*");
            await ScanAndDeleteAsync($"scope-tree:{tenantId}");
            await PublishInvalidationAsync(tenantId);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex,
                "Redis error during full tenant key invalidation for tenant {TenantId}", tenantId);
        }
    }

    // ── roles:{tenantId}:{userId}  TTL 300s ──────────────────────────────────

    private static readonly TimeSpan RolesTtl = TimeSpan.FromSeconds(300);

    private static string RolesKey(Guid tenantId, Guid userId)
        => $"roles:{tenantId}:{userId}";

    public async Task<IReadOnlyList<string>?> GetUserPermissionCodesAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            var val = await _db.StringGetAsync(RolesKey(tenantId, userId));
            if (!val.HasValue) return null;
            return JsonSerializer.Deserialize<List<string>>(val.ToString());
        }
        catch (RedisException ex) { return HandleRedisFailure<IReadOnlyList<string>?>(ex, null); }
    }

    public async Task SetUserPermissionCodesAsync(
        Guid userId, Guid tenantId, IReadOnlyList<string> codes, CancellationToken ct = default)
    {
        try
        {
            await _db.StringSetAsync(
                RolesKey(tenantId, userId),
                JsonSerializer.Serialize(codes),
                RolesTtl);
        }
        catch (RedisException ex) { HandleRedisFailure<bool>(ex, false); }
    }

    // ── scope-tree:{tenantId}:{scopeId}  TTL 3600s ───────────────────────────

    private static readonly TimeSpan ScopeTreeTtl = TimeSpan.FromSeconds(3600);

    private static string ScopeTreeKey(Guid tenantId, Guid scopeId)
        => $"scope-tree:{tenantId}:{scopeId}";

    public async Task<IReadOnlyList<Guid>?> GetScopeAncestorsAsync(
        Guid scopeId, Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            var val = await _db.StringGetAsync(ScopeTreeKey(tenantId, scopeId));
            if (!val.HasValue) return null;
            return JsonSerializer.Deserialize<List<Guid>>(val.ToString());
        }
        catch (RedisException ex) { return HandleRedisFailure<IReadOnlyList<Guid>?>(ex, null); }
    }

    public async Task SetScopeAncestorsAsync(
        Guid scopeId, Guid tenantId, IReadOnlyList<Guid> ancestorIds, CancellationToken ct = default)
    {
        try
        {
            await _db.StringSetAsync(
                ScopeTreeKey(tenantId, scopeId),
                JsonSerializer.Serialize(ancestorIds),
                ScopeTreeTtl);
        }
        catch (RedisException ex) { HandleRedisFailure<bool>(ex, false); }
    }

    // ── delegation:{tenantId}:{userId}  TTL 60s ──────────────────────────────

    private static readonly TimeSpan DelegationTtl = TimeSpan.FromSeconds(60);

    private static string DelegationKey(Guid tenantId, Guid userId)
        => $"delegation:{tenantId}:{userId}";

    public async Task<string?> GetDelegationJsonAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            var val = await _db.StringGetAsync(DelegationKey(tenantId, userId));
            return val.HasValue ? val.ToString() : null;
        }
        catch (RedisException ex) { return HandleRedisFailure<string?>(ex, null); }
    }

    public async Task SetDelegationJsonAsync(
        Guid userId, Guid tenantId, string delegationJson, CancellationToken ct = default)
    {
        try
        {
            await _db.StringSetAsync(
                DelegationKey(tenantId, userId),
                delegationJson,
                DelegationTtl);
        }
        catch (RedisException ex) { HandleRedisFailure<bool>(ex, false); }
    }

    // ── ITokenVersionService ──────────────────────────────────────────────────

    public async Task<int> GetTokenVersionAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var key = TokenVersionKey(userId);
            var val = await _db.StringGetAsync(key);

            if (!val.HasValue)
                return 0;

            // Refresh sliding TTL on every read
            await _db.KeyExpireAsync(key, TokenVersionTtl);

            return (int)val;
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex,
                "Redis unavailable when reading token version for user {UserId}. Returning 0.", userId);
            return 0;
        }
    }

    public async Task IncrementTokenVersionAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var key = TokenVersionKey(userId);
            await _db.StringIncrementAsync(key);
            await _db.KeyExpireAsync(key, TokenVersionTtl);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex,
                "Redis unavailable when incrementing token version for user {UserId}", userId);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// SCAN for keys matching <paramref name="pattern"/>, then DEL them in batches.
    /// This is O(N) but necessary for pattern-based invalidation.
    /// For high-cardinality tenants, consider using a generation counter instead.
    /// </summary>
    private async Task ScanAndDeleteAsync(string pattern)
    {
        foreach (var endpoint in _mux.GetEndPoints())
        {
            var server = _mux.GetServer(endpoint);
            var keys   = server.KeysAsync(pattern: pattern, pageSize: 100);

            var batch = new List<RedisKey>(100);
            await foreach (var key in keys)
            {
                batch.Add(key);
                if (batch.Count >= 100)
                {
                    await _db.KeyDeleteAsync(batch.ToArray());
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
                await _db.KeyDeleteAsync(batch.ToArray());
        }
    }

    /// <summary>
    /// Publishes an invalidation notification to the Redis pub/sub channel
    /// <c>cache-invalidation:{tenantId}</c>.
    /// All app instances that subscribe will bust their local L1 in-memory
    /// cache entries for this tenant.
    /// </summary>
    private async Task PublishInvalidationAsync(Guid tenantId)
    {
        var channel = new RedisChannel(InvalidationChannel(tenantId), RedisChannel.PatternMode.Literal);
        await _mux.GetSubscriber().PublishAsync(channel, tenantId.ToString());
    }

    /// <summary>
    /// Handles a Redis failure according to the configured failure mode.
    /// • allow-through (FailClosed=false): logs a warning and returns <paramref name="allowThroughValue"/>.
    /// • fail-closed   (FailClosed=true):  logs an error and throws so the caller returns Denied.
    /// </summary>
    private T HandleRedisFailure<T>(RedisException ex, T allowThroughValue)
    {
        if (_options.FailClosed)
        {
            _logger.LogError(ex, "Redis unavailable (fail-closed mode). Denying request.");
            throw new CacheUnavailableException("Redis unavailable (fail-closed mode).", ex);
        }

        _logger.LogWarning(ex,
            "Redis unavailable (allow-through mode). Falling back to database evaluation.");
        return allowThroughValue;
    }

    // ── Private types ─────────────────────────────────────────────────────────

    private sealed record CachedAccessResult(
        bool   IsGranted,
        int?   DenialReason,
        int    TokenVersion);
}
