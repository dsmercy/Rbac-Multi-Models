using Delegation.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace RbacSystem.Api.Controllers;

[ApiController]
[Route("api/v1/tenants/{tid:guid}/delegations")]
[Authorize]
public sealed class DelegationsController : ControllerBase
{
    private readonly ISender _sender;

    public DelegationsController(ISender sender) => _sender = sender;

    [HttpPost]
    public async Task<IActionResult> CreateDelegation(
        Guid tid,
        [FromBody] CreateDelegationRequest request,
        CancellationToken ct)
    {
        var callerId = GetCallerId();

        var id = await _sender.Send(new CreateDelegationCommand(
            tid,
            callerId,
            request.DelegateeId,
            request.PermissionCodes,
            request.ScopeId,
            request.ExpiresAt,
            callerId), ct);

        return Created($"api/v1/tenants/{tid}/delegations/{id}", new { id });
    }

    [HttpDelete("{did:guid}")]
    public async Task<IActionResult> RevokeDelegation(
        Guid tid, Guid did, CancellationToken ct)
    {
        await _sender.Send(new RevokeDelegationCommand(tid, did, GetCallerId()), ct);
        return NoContent();
    }

    private Guid GetCallerId()
    {
        var claim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public sealed record CreateDelegationRequest(
    Guid DelegateeId,
    IReadOnlyList<string> PermissionCodes,
    Guid ScopeId,
    DateTimeOffset ExpiresAt);
