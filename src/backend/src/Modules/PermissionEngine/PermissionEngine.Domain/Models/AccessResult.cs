namespace PermissionEngine.Domain.Models;

/// <summary>
/// Immutable result returned by CanUserAccess.
/// Carries the access decision plus all metadata required for structured
/// audit logging and client-side diagnostics.
/// </summary>
public sealed class AccessResult
{
    // ── Decision ──────────────────────────────────────────────────────────────

    public bool IsGranted { get; private init; }

    /// <summary>
    /// Set when IsGranted = false. Indicates which pipeline step produced
    /// the denial and why. Used for structured audit logs and metrics labels.
    /// </summary>
    public DenialReason? Reason { get; private init; }

    // ── Diagnostics ───────────────────────────────────────────────────────────

    /// <summary>Whether this result was served from the Redis cache.</summary>
    public bool CacheHit { get; private init; }

    /// <summary>
    /// Total wall-clock time from pipeline entry to result production (ms).
    /// Target: &lt;5ms for cache hits, &lt;50ms for full pipeline evaluation.
    /// </summary>
    public long EvaluationLatencyMs { get; private init; }

    /// <summary>
    /// ID of the policy that produced a DENY, if applicable.
    /// Null for denials from RBAC or delegation steps.
    /// </summary>
    public string? MatchedPolicyId { get; private init; }

    /// <summary>
    /// Delegation chain metadata when access was granted (or denied) via
    /// an active delegation. Null for direct role-based access.
    /// Logged in audit records as ActingOnBehalfOf.
    /// </summary>
    public DelegationChainInfo? DelegationChain { get; private init; }

    // ── Private constructor — use factory methods ─────────────────────────────

    private AccessResult() { }

    // ── Factory methods ───────────────────────────────────────────────────────

    public static AccessResult Granted(
        bool cacheHit,
        long latencyMs,
        DelegationChainInfo? delegationChain = null)
        => new()
        {
            IsGranted = true,
            CacheHit = cacheHit,
            EvaluationLatencyMs = latencyMs,
            DelegationChain = delegationChain
        };

    /// <summary>
    /// Constructs a Denied result from full pipeline evaluation.
    /// </summary>
    public static AccessResult Denied(
        DenialReason reason,
        long latencyMs,
        string? message = null,
        string? matchedPolicyId = null)
        => new()
        {
            IsGranted = false,
            Reason = reason,
            EvaluationLatencyMs = latencyMs,
            MatchedPolicyId = matchedPolicyId
        };

    /// <summary>
    /// Constructs a Denied result from a cached entry — no latency to report.
    /// </summary>
    public static AccessResult DeniedFromCache(DenialReason reason)
        => new()
        {
            IsGranted = false,
            Reason = reason,
            CacheHit = true,
            EvaluationLatencyMs = 0
        };
}
