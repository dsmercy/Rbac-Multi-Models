namespace PermissionEngine.Infrastructure.Cache;

/// <summary>
/// Configuration options for the permission cache and its failure behaviour.
/// Bind from <c>PermissionCache</c> section in appsettings.
/// </summary>
public sealed class PermissionCacheOptions
{
    public const string SectionName = "PermissionCache";

    /// <summary>
    /// When true (fail-closed / secure mode): if Redis is unreachable,
    /// CanUserAccess returns Denied(RedisUnavailable) rather than falling
    /// through to database evaluation.
    ///
    /// Default: false (allow-through / available mode).
    /// Rationale: in most deployments, availability > strict security;
    /// the database still enforces row-level tenant isolation.
    /// Override to true in high-security or compliance-mandated deployments.
    /// </summary>
    public bool FailClosed { get; set; } = false;

    /// <summary>
    /// Maximum wall-clock time (ms) allowed for the full evaluation pipeline.
    /// Requests that exceed this budget are denied rather than allowed to
    /// block indefinitely.
    /// Default: 200 ms (per spec).
    /// </summary>
    public int EvaluationTimeoutMs { get; set; } = 200;

    /// <summary>
    /// Duration (ms) of the SET NX stampede-protection lock.
    /// A parallel request that loses the race waits this long before
    /// retrying the cache read. Default: 2000 ms (per spec).
    /// </summary>
    public int StampedeLockMs { get; set; } = 2000;

    /// <summary>
    /// TTL (seconds) for entries stored in the in-process L1 memory cache.
    /// L1 sits in front of Redis (L2) to avoid network round-trips on hot paths.
    /// L1 entries are also evicted immediately on any cache-invalidation pub/sub message.
    /// Default: 5 s — short enough that staleness window is minimal; long enough
    /// to absorb bursts of identical permission checks within a single request fan-out.
    /// </summary>
    public int L1CacheTtlSeconds { get; set; } = 5;
}
