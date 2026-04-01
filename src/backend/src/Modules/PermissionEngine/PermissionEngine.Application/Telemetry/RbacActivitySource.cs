using System.Diagnostics;

namespace PermissionEngine.Application.Telemetry;

/// <summary>
/// Central ActivitySource for OpenTelemetry distributed tracing across the RBAC
/// permission evaluation pipeline.
///
/// Consumers call <c>RbacActivitySource.Source.StartActivity(...)</c> to create
/// child spans. The source is registered in Program.cs via:
///   <c>.AddSource(RbacActivitySource.Name)</c>
/// and exported to Jaeger (dev) / Azure Monitor (prod) via the OTLP exporter.
///
/// Span naming convention:  rbac.{component}.{operation}
/// </summary>
public static class RbacActivitySource
{
    public const string Name = "RbacSystem.PermissionEngine";

    /// <summary>Shared ActivitySource instance — created once, used everywhere.</summary>
    public static readonly ActivitySource Source = new(Name, "1.0.0");

    // ── Span name constants ────────────────────────────────────────────────────

    public const string SpanEval              = "rbac.eval";
    public const string SpanCacheLookup       = "rbac.cache.lookup";
    public const string SpanCacheWrite        = "rbac.cache.write";
    public const string SpanPipelineStep      = "rbac.pipeline.step";
    public const string SpanDbQuery           = "rbac.db.query";

    // ── Tag key constants ──────────────────────────────────────────────────────

    public const string TagTenantId           = "rbac.tenant_id";
    public const string TagUserId             = "rbac.user_id";
    public const string TagAction             = "rbac.action";
    public const string TagResourceId         = "rbac.resource_id";
    public const string TagScopeId            = "rbac.scope_id";
    public const string TagResult             = "rbac.result";
    public const string TagDeniedReason       = "rbac.denied_reason";
    public const string TagCacheHit           = "rbac.cache_hit";
    public const string TagEvalLatencyMs      = "rbac.eval_latency_ms";
    public const string TagStepName           = "rbac.step_name";
    public const string TagStepOrder          = "rbac.step_order";
    public const string TagDelegationChainId  = "rbac.delegation_chain_id";
}
