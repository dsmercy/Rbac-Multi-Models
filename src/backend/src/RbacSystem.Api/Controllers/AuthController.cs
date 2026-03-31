using Identity.Application.Commands;
using Identity.Application.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RbacSystem.Api.Controllers;

/// <summary>
/// Authentication — issue and refresh JWT access tokens.
/// All endpoints are anonymous (no JWT required).
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender) => _sender = sender;

    /// <summary>Authenticate with email and password and receive a JWT token pair.</summary>
    /// <remarks>
    /// Returns a short-lived access token (15 min) and a sliding refresh token (7 days).
    /// The <c>tv</c> (token version) claim in the access token is read from Redis and must
    /// match on every permission-engine call. Role/delegation changes increment it,
    /// invalidating in-flight tokens.
    /// </remarks>
    /// <param name="request">Tenant ID, email address, and password.</param>
    /// <response code="200">Authentication successful. Returns access + refresh token pair.</response>
    /// <response code="400">Request validation failed (missing fields, invalid email format).</response>
    /// <response code="401">Invalid credentials, inactive account, or account locked.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _sender.Send(
            new LoginCommand(request.TenantId, request.Email, request.Password, ip), ct);

        return Ok(result);
    }

    /// <summary>Exchange a valid refresh token for a new access token.</summary>
    /// <remarks>
    /// The refresh token is single-use sliding (7-day window). Expired or revoked
    /// refresh tokens return <c>401</c>. A stale <c>tv</c> claim also returns <c>401</c>
    /// and revokes all refresh tokens for the user, forcing full re-login.
    /// </remarks>
    /// <param name="request">Raw refresh token string and the tenant ID it belongs to.</param>
    /// <response code="200">Refresh successful. Returns a new access + refresh token pair.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Refresh token expired, revoked, or token version stale.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new RefreshTokenCommand(request.RefreshToken, request.TenantId), ct);

        return Ok(result);
    }
}

/// <summary>Login request payload.</summary>
public sealed record LoginRequest(
    /// <summary>ID of the tenant the user belongs to.</summary>
    Guid TenantId,
    /// <summary>User's email address.</summary>
    string Email,
    /// <summary>User's plaintext password (transmitted over TLS only).</summary>
    string Password);

/// <summary>Token refresh request payload.</summary>
public sealed record RefreshRequest(
    /// <summary>Raw (unhashed) refresh token string returned from a previous login or refresh.</summary>
    string RefreshToken,
    /// <summary>Tenant ID that the refresh token is scoped to.</summary>
    Guid TenantId);
