using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PermissionEngine.Application.Telemetry;

/// <summary>
/// All Prometheus / OpenTelemetry metrics for the RBAC permission evaluation engine.
///
/// Uses <c>System.Diagnostics.Metrics</c> (built-in .NET 8) — no proprietary SDK.
/// The OTel SDK's <c>.AddMeter(RbacMetrics.MeterName)</c> wires these into the
/// Prometheus exporter at <c>/metrics</c>.
///
/// Metrics defined here (per CLAUDE.md spec):
///   rbac_eval_total{result, cache_hit, tenant_id}      — counter
///   rbac_eval_duration_ms                               — histogram (buckets: 5,10,25,50,100,200,500ms)
///   rbac_active_delegations{tenant_id}                  — up/down counter
///   rbac_cache_evictions_total{key_type}                — counter
///   rbac_policy_eval_errors_total                       — counter
///   rbac_delegation_chain_max_depth_total               — counter
/// </summary>
public static class RbacMetrics
{
    public const string MeterName = "RbacSystem";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    // ── rbac_eval_total ────────────────────────────────────────────────────────
    // Incremented once per CanUserAccessAsync call with result + cache_hit labels.
    public static readonly Counter<long> EvalTotal =
        _meter.CreateCounter<long>(
            "rbac_eval_total",
            unit:        "requests",
            description: "Total number of permission evaluations by result and cache hit status.");

    // ── rbac_eval_duration_ms ──────────────────────────────────────────────────
    // P50 target: <5ms (cache hit), <50ms (cache miss)
    // P95 target: <10ms (cache hit), <100ms (cache miss)
    // P99 target: <20ms (cache hit), <200ms (cache miss)
    public static readonly Histogram<double> EvalDuration =
        _meter.CreateHistogram<double>(
            "rbac_eval_duration_ms",
            unit:        "ms",
            description: "Permission evaluation latency in milliseconds.");

    // ── rbac_active_delegations ────────────────────────────────────────────────
    // Incremented on DelegationCreated, decremented on DelegationRevoked/Expired.
    // Labelled by tenant_id so Grafana can chart per-tenant delegation load.
    public static readonly UpDownCounter<long> ActiveDelegations =
        _meter.CreateUpDownCounter<long>(
            "rbac_active_delegations",
            unit:        "delegations",
            description: "Number of currently active delegations per tenant.");

    // ── rbac_cache_evictions_total ─────────────────────────────────────────────
    // Labelled by key_type: "perm", "roles", "policy", "scope-tree", "delegation", "token-version"
    public static readonly Counter<long> CacheEvictions =
        _meter.CreateCounter<long>(
            "rbac_cache_evictions_total",
            unit:        "evictions",
            description: "Total number of cache key evictions by key type.");

    // ── rbac_policy_eval_errors_total ──────────────────────────────────────────
    // Incremented when an individual policy rule throws during ABAC evaluation.
    public static readonly Counter<long> PolicyEvalErrors =
        _meter.CreateCounter<long>(
            "rbac_policy_eval_errors_total",
            unit:        "errors",
            description: "Total number of errors during ABAC policy evaluation.");

    // ── rbac_delegation_chain_max_depth_total ──────────────────────────────────
    // Incremented when a delegation chain at the configured maximum depth is detected.
    // Used by the alert: rbac_delegation_chain_max_depth_total > 0.
    public static readonly Counter<long> DelegationChainMaxDepth =
        _meter.CreateCounter<long>(
            "rbac_delegation_chain_max_depth_total",
            unit:        "occurrences",
            description: "Number of times a delegation chain at the configured maximum depth was detected.");

    // ── Helper methods ─────────────────────────────────────────────────────────

    /// <summary>
    /// Records a completed permission evaluation with all standard labels.
    /// Call this once per CanUserAccessAsync call after the result is known.
    /// </summary>
    public static void RecordEval(
        bool   granted,
        bool   cacheHit,
        string tenantId,
        double latencyMs)
    {
        var tags = new TagList
        {
            { "result",    granted   ? "granted" : "denied" },
            { "cache_hit", cacheHit  ? "true"    : "false"  },
            { "tenant_id", tenantId }
        };

        EvalTotal.Add(1, tags);
        EvalDuration.Record(latencyMs, new TagList { { "tenant_id", tenantId } });
    }

    /// <summary>Records a single cache key eviction. keyType should be one of the standard key-type labels.</summary>
    public static void RecordCacheEviction(string keyType, string tenantId)
        => CacheEvictions.Add(1, new TagList { { "key_type", keyType }, { "tenant_id", tenantId } });

    /// <summary>Increments active delegation count when a delegation is created.</summary>
    public static void DelegationCreated(string tenantId)
        => ActiveDelegations.Add(1, new TagList { { "tenant_id", tenantId } });

    /// <summary>Decrements active delegation count when a delegation is revoked or expires.</summary>
    public static void DelegationEnded(string tenantId)
        => ActiveDelegations.Add(-1, new TagList { { "tenant_id", tenantId } });
}
