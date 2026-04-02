using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;

namespace RbacSystem.Api.Controllers;

/// <summary>
/// Permission evaluation — invoke the full 7-step permission engine pipeline and
/// receive a detailed <c>AccessResult</c> including cache hit status, evaluation latency,
/// and the delegation chain used (if any).
/// </summary>
[ApiController]
[Route("api/v1/tenants/{tid:guid}/permissions")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionCheckController : ControllerBase
{
    private readonly IPermissionEngine _permissionEngine;

    public PermissionCheckController(IPermissionEngine permissionEngine)
        => _permissionEngine = permissionEngine;

    /// <summary>Check whether a user has access to perform an action on a resource within a scope.</summary>
    /// <remarks>
    /// Runs the full evaluation pipeline in order:
    /// <list type="number">
    ///   <item>Token version validation (Redis check — rejects stale JWT).</item>
    ///   <item>Explicit global deny (active Deny policy on tenant/resource).</item>
    ///   <item>Resource-level override (direct grant or deny on exact ResourceId).</item>
    ///   <item>Delegation check (active, non-expired, chain depth ≤ max).</item>
    ///   <item>Scope inheritance (walk ScopeHierarchy closure table upward).</item>
    ///   <item>ABAC policy evaluation (condition tree matching).</item>
    ///   <item>Role-based permission check (deny-overrides-allow).</item>
    ///   <item>Default deny.</item>
    /// </list>
    /// <para>
    /// Cache: results are cached in Redis at <c>perm:{tid}:{uid}:{action}:{resourceType}:{scopeId}</c>
    /// (TTL 60 s by default). The <c>cacheHit</c> field in the response indicates whether the
    /// result was served from cache.
    /// </para>
    /// <para>
    /// Environment attributes (<c>time_utc</c>, <c>date_utc</c>, <c>ip</c>) are injected
    /// server-side from the incoming HTTP request context.
    /// </para>
    /// </remarks>
    /// <param name="tid">Tenant UUID (must match the caller's JWT <c>tid</c> claim).</param>
    /// <param name="request">User, action, resource, scope, and optional ABAC attribute overrides.</param>
    /// <response code="200">Evaluation complete. <c>isGranted</c> is <c>false</c> for denied results too — check <c>denialReason</c>.</response>
    /// <response code="400">Request validation failed (empty UserId, missing action, empty ResourceId).</response>
    /// <response code="401">Missing/invalid JWT, or token version is stale (<c>TOKEN_STALE</c>) — re-authenticate required.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="503">Permission engine timed out or database is unavailable — the caller must distinguish this from a denial.</response>
    [HttpPost("check")]
    [ProducesResponseType(typeof(PermissionCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CheckPermission(
        Guid tid,
        [FromBody] PermissionCheckRequest request,
        CancellationToken ct)
    {
        var validationErrors = request.Validate().ToList();
        if (validationErrors.Count > 0)
            return BadRequest(new { errors = validationErrors.Select(e => new { field = e.MemberNames.FirstOrDefault(), message = e.ErrorMessage }) });

        var tokenVersionClaim = User.FindFirst("tv")?.Value;
        int? tokenVersion = tokenVersionClaim is not null
            && int.TryParse(tokenVersionClaim, out var tv)
            ? tv
            : null;

        var correlationId = HttpContext.TraceIdentifier is { Length: > 0 } traceId
            ? Guid.TryParse(traceId, out var parsed) ? parsed : Guid.NewGuid()
            : Guid.NewGuid();

        var context = new EvaluationContext(
            tenantId: tid,
            correlationId: correlationId,
            userAttributes: request.UserAttributes ?? new Dictionary<string, object>(),
            resourceAttributes: request.ResourceAttributes ?? new Dictionary<string, object>(),
            environmentAttributes: BuildEnvironmentAttributes(),
            tokenVersion: tokenVersion);

        // ResourceId is optional — null means a resource-type-level check (no specific instance).
        var resourceId = request.ResourceId ?? Guid.Empty;

        var result = await _permissionEngine.CanUserAccessAsync(
            request.UserId, request.Action, resourceId, request.ScopeId, context, ct);

        return Ok(new PermissionCheckResponse(
            result.IsGranted,
            result.Reason?.ToString(),
            result.CacheHit,
            result.EvaluationLatencyMs,
            result.DelegationChain is not null
                ? $"{result.DelegationChain.DelegatorId}→{result.DelegationChain.DelegateeId}"
                : null));
    }

    private Dictionary<string, object> BuildEnvironmentAttributes() => new()
    {
        ["time_utc"] = DateTimeOffset.UtcNow.ToString("HH:mm"),
        ["date_utc"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
        ["ip"]       = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
    };
}

/// <summary>Permission check request.</summary>
public sealed record PermissionCheckRequest(
    /// <summary>UUID of the user to check access for.</summary>
    Guid UserId,
    /// <summary>Action string to check (e.g. <c>users:delete</c>, <c>reports:export</c>).</summary>
    string Action,
    /// <summary>
    /// UUID of the specific resource instance being accessed.
    /// Omit (null) for resource-type-level checks where no specific instance is targeted.
    /// </summary>
    Guid? ResourceId,
    /// <summary>UUID of the scope within which the check is performed.</summary>
    Guid ScopeId,
    /// <summary>Optional resource type label (informational — used for logging/audit context).</summary>
    string? ResourceType,
    /// <summary>Optional ABAC user attributes to supplement or override JWT-embedded claims during evaluation.</summary>
    IDictionary<string, object>? UserAttributes,
    /// <summary>Optional ABAC resource attributes used in condition tree evaluation.</summary>
    IDictionary<string, object>? ResourceAttributes)
{
    /// <summary>Validates required fields before dispatching to the permission engine.</summary>
    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate()
    {
        if (UserId == Guid.Empty)
            yield return new("UserId must be a non-empty GUID.", new[] { nameof(UserId) });
        if (string.IsNullOrWhiteSpace(Action))
            yield return new("Action is required.", new[] { nameof(Action) });
    }
}

/// <summary>Permission evaluation result.</summary>
public sealed record PermissionCheckResponse(
    /// <summary><c>true</c> = access granted; <c>false</c> = access denied.</summary>
    bool IsGranted,
    /// <summary>Machine-readable reason for denial (e.g. <c>DefaultDeny</c>, <c>AbacPolicyDeny</c>, <c>ExplicitGlobalDeny</c>). Null when granted.</summary>
    string? DenialReason,
    /// <summary><c>true</c> if the result was served from the Redis / L1 cache without running the full pipeline.</summary>
    bool CacheHit,
    /// <summary>Wall-clock time in milliseconds for the evaluation (includes cache lookup).</summary>
    long EvaluationLatencyMs,
    /// <summary>
    /// Delegation chain used, formatted as <c>delegatorId→delegateeId</c>.
    /// Null when the result was not reached via delegation.
    /// </summary>
    string? DelegationChain);
