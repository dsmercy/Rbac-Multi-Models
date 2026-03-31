using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PolicyEngine.Application.Commands;
using PolicyEngine.Domain.Entities;
using System.Security.Claims;

namespace RbacSystem.Api.Controllers;

[ApiController]
[Route("api/v1/tenants/{tid:guid}/policies")]
[Authorize]
public sealed class PoliciesController : ControllerBase
{
    private readonly ISender _sender;

    public PoliciesController(ISender sender) => _sender = sender;

    [HttpPost]
    public async Task<IActionResult> CreatePolicy(
        Guid tid,
        [FromBody] CreatePolicyRequest request,
        CancellationToken ct)
    {
        var id = await _sender.Send(new CreatePolicyCommand(
            tid,
            request.Name,
            request.Description,
            request.Effect,
            request.ConditionTreeJson,
            request.ResourceId,
            request.Action,
            GetCallerId()), ct);

        return Created($"api/v1/tenants/{tid}/policies/{id}", new { id });
    }

    [HttpDelete("{policyId:guid}")]
    public async Task<IActionResult> DeletePolicy(
        Guid tid, Guid policyId, CancellationToken ct)
    {
        await _sender.Send(new DeletePolicyCommand(tid, policyId, GetCallerId()), ct);
        return NoContent();
    }

    private Guid GetCallerId()
    {
        var claim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public sealed record CreatePolicyRequest(
    string Name,
    string? Description,
    PolicyEffect Effect,
    string ConditionTreeJson,
    Guid? ResourceId,
    string? Action);
