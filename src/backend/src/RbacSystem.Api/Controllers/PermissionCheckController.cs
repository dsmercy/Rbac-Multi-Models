using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using System.Security.Claims;

namespace RbacSystem.Api.Controllers;

/// <summary>
/// Exposes the permission evaluation engine as an HTTP endpoint.
///
/// Phase 4 additions:
///   • Extracts the "tv" (token version) claim from the caller's JWT and
///     passes it into EvaluationContext so TokenVersionValidationStep (step 0)
///     can detect stale tokens.
///   • If the token version is stale, TokenVersionValidationStep throws
///     StaleTokenException → GlobalExceptionMiddleware → 401.
///   • Environment attributes (time_utc, date_utc, ip) are populated here
///     for ABAC policy evaluation.
/// </summary>
[ApiController]
[Route("api/v1/tenants/{tid:guid}/permissions")]
[Authorize]
public sealed class PermissionCheckController : ControllerBase
{
    private readonly IPermissionEngine _permissionEngine;

    public PermissionCheckController(IPermissionEngine permissionEngine)
        => _permissionEngine = permissionEngine;

    [HttpPost("check")]
    public async Task<IActionResult> CheckPermission(
        Guid tid,
        [FromBody] PermissionCheckRequest request,
        CancellationToken ct)
    {
        var validationErrors = request.Validate().ToList();
        if (validationErrors.Count > 0)
            return BadRequest(new { errors = validationErrors.Select(e => new { field = e.MemberNames.FirstOrDefault(), message = e.ErrorMessage }) });

        // Extract the "tv" (token version) claim from the caller's JWT.
        // TokenVersionValidationStep (step 0) compares this against Redis and
        // throws StaleTokenException (→ 401) if the versions differ.
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

        var result = await _permissionEngine.CanUserAccessAsync(
            request.UserId,
            request.Action,
            request.ResourceId,
            request.ScopeId,
            context,
            ct);

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

public sealed record PermissionCheckRequest(
    Guid UserId,
    string Action,
    Guid ResourceId,
    Guid ScopeId,
    IDictionary<string, object>? UserAttributes,
    IDictionary<string, object>? ResourceAttributes)
{
    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate()
    {
        if (UserId == Guid.Empty)
            yield return new("UserId must be a non-empty GUID.", new[] { nameof(UserId) });
        if (string.IsNullOrWhiteSpace(Action))
            yield return new("Action is required.", new[] { nameof(Action) });
        if (ResourceId == Guid.Empty)
            yield return new("ResourceId must be a non-empty GUID.", new[] { nameof(ResourceId) });
    }
}

public sealed record PermissionCheckResponse(
    bool IsGranted,
    string? DenialReason,
    bool CacheHit,
    long EvaluationLatencyMs,
    string? DelegationChain);
