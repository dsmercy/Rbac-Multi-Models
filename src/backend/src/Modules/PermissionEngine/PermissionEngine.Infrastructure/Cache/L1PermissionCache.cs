using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using PermissionEngine.Domain.Models;
using System.Collections.Concurrent;

namespace PermissionEngine.Infrastructure.Cache;

/// <summary>
/// In-process L1 cache for permission check results.
/// Sits in front of Redis (L2) to avoid a network round-trip on hot paths.
///
/// Invalidation is per-tenant: calling <see cref="InvalidateTenant"/> cancels
/// a <see cref="CancellationTokenSource"/> shared by every entry stored for that
/// tenant.  All affected entries are evicted by the <see cref="IMemoryCache"/>
/// change-token machinery without iterating the full cache.
///
/// This type is registered as Singleton — one instance per process.
/// </summary>
public sealed class L1PermissionCache : IDisposable
{
    private readonly IMemoryCache _inner;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _tenantCts = new();

    public L1PermissionCache(IMemoryCache inner) => _inner = inner;

    // ── Read ──────────────────────────────────────────────────────────────────

    public bool TryGet(string key, out AccessResult? result)
        => _inner.TryGetValue(key, out result);

    // ── Write ─────────────────────────────────────────────────────────────────

    public void Set(string key, Guid tenantId, AccessResult result, TimeSpan ttl)
    {
        var token = GetOrCreateToken(tenantId);

        using var entry = _inner.CreateEntry(key);
        entry.Value                            = result;
        entry.AbsoluteExpirationRelativeToNow = ttl;
        entry.AddExpirationToken(new CancellationChangeToken(token));
    }

    // ── Invalidation ──────────────────────────────────────────────────────────

    /// <summary>
    /// Evicts all L1 entries for <paramref name="tenantId"/> by cancelling
    /// the per-tenant <see cref="CancellationTokenSource"/>.
    /// </summary>
    public void InvalidateTenant(Guid tenantId)
    {
        if (_tenantCts.TryRemove(tenantId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private CancellationToken GetOrCreateToken(Guid tenantId)
        => _tenantCts.GetOrAdd(tenantId, _ => new CancellationTokenSource()).Token;

    public void Dispose()
    {
        foreach (var cts in _tenantCts.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
