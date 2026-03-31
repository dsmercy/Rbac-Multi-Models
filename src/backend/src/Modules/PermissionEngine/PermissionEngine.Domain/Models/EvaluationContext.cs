namespace PermissionEngine.Domain.Models;

/// <summary>
/// Carries all per-request contextual data through the 8-step permission
/// evaluation pipeline.
///
/// Immutable after construction except for mutable attribute dictionaries
/// (which are populated before the first pipeline step runs).
///
/// TokenVersion:
///   The "tv" claim extracted from the caller's JWT.
///   Null for server-to-server calls that do not carry a user JWT —
///   TokenVersionValidationStep skips validation when this is null.
/// </summary>
public sealed class EvaluationContext
{
    /// <summary>The tenant this evaluation belongs to.</summary>
    public Guid TenantId { get; }

    /// <summary>
    /// Distributed tracing correlation ID — propagated from the incoming
    /// HTTP request's traceparent / X-Correlation-ID header.
    /// </summary>
    public Guid CorrelationId { get; }

    /// <summary>
    /// Attributes about the calling user (e.g. department, clearance level).
    /// Keys use dot notation: "user.department", "user.clearance".
    /// </summary>
    public IDictionary<string, object> UserAttributes { get; }

    /// <summary>
    /// Attributes about the target resource (e.g. classification, owner).
    /// Keys: "resource.classification", "resource.owner_id".
    /// </summary>
    public IDictionary<string, object> ResourceAttributes { get; }

    /// <summary>
    /// Attributes about the environment (e.g. request time, IP, region).
    /// Keys: "env.time_utc", "env.ip", "env.date_utc".
    /// Populated by the API layer from HttpContext before pipeline entry.
    /// </summary>
    public IDictionary<string, object> EnvironmentAttributes { get; }

    /// <summary>
    /// Token version embedded in the caller's JWT as the "tv" claim.
    /// Null for server-to-server calls that bypass JWT validation.
    /// Used by TokenVersionValidationStep (pipeline step 0) to detect
    /// stale tokens after role/delegation changes.
    /// </summary>
    public int? TokenVersion { get; }

    public EvaluationContext(
        Guid tenantId,
        Guid correlationId,
        IDictionary<string, object>? userAttributes = null,
        IDictionary<string, object>? resourceAttributes = null,
        IDictionary<string, object>? environmentAttributes = null,
        int? tokenVersion = null)
    {
        TenantId = tenantId;
        CorrelationId = correlationId;
        UserAttributes = userAttributes ?? new Dictionary<string, object>();
        ResourceAttributes = resourceAttributes ?? new Dictionary<string, object>();
        EnvironmentAttributes = environmentAttributes ?? new Dictionary<string, object>();
        TokenVersion = tokenVersion;
    }
}
