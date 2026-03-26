using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using System.Security.Claims;

namespace RbacSystem.Api.Controllers;

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
        var context = new EvaluationContext(
            tenantId: tid,
            correlationId: HttpContext.TraceIdentifier.GetHashCode() is var h
                ? Guid.NewGuid()
                : Guid.NewGuid(),
            userAttributes: request.UserAttributes ?? new Dictionary<string, object>(),
            resourceAttributes: request.ResourceAttributes ?? new Dictionary<string, object>(),
            environmentAttributes: BuildEnvironmentAttributes());

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
        ["ip"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
    };
}

public sealed record PermissionCheckRequest(
    Guid UserId,
    string Action,
    Guid ResourceId,
    Guid ScopeId,
    IDictionary<string, object>? UserAttributes,
    IDictionary<string, object>? ResourceAttributes);

public sealed record PermissionCheckResponse(
    bool IsGranted,
    string? DenialReason,
    bool CacheHit,
    long EvaluationLatencyMs,
    string? DelegationChain);
