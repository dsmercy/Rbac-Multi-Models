using Identity.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RbacSystem.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender) => _sender = sender;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _sender.Send(
            new LoginCommand(request.TenantId, request.Email, request.Password, ip), ct);

        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new RefreshTokenCommand(request.RefreshToken, request.TenantId), ct);

        return Ok(result);
    }
}

public sealed record LoginRequest(Guid TenantId, string Email, string Password);
public sealed record RefreshRequest(string RefreshToken, Guid TenantId);
