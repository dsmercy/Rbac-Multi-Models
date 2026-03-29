using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantManagement.Application.Commands;
using TenantManagement.Application.Queries;
using TenantManagement.Application.Services;

namespace RbacSystem.Api.Controllers;

[ApiController]
[Route("api/v1/tenants")]
//[Authorize]
public sealed class TenantsController : ControllerBase
{
    private readonly ISender _sender;

    public TenantsController(ISender sender) => _sender = sender;

    [HttpPost]
    [Authorize(Roles = "platform:super-admin")]
    public async Task<IActionResult> CreateTenant(
        [FromBody] CreateTenantRequest request,
        CancellationToken ct)
    {
        var callerUserId = GetCallerId();

        var result = await _sender.Send(new CreateTenantCommand(
            request.Name,
            request.Slug,
            request.AdminEmail,
            request.AdminPassword,
            callerUserId), ct);

        return CreatedAtAction(nameof(GetTenant), new { tid = result.Id }, result);
    }

    [HttpGet("{tid:guid}")]
    public async Task<IActionResult> GetTenant(Guid tid, CancellationToken ct)
    {
        var result = await _sender.Send(new GetTenantByIdQuery(tid), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{tid:guid}/suspend")]
    [Authorize(Roles = "platform:super-admin")]
    public async Task<IActionResult> SuspendTenant(
        Guid tid,
        [FromBody] SuspendTenantRequest request,
        CancellationToken ct)
    {
        await _sender.Send(new SuspendTenantCommand(tid, request.Reason, GetCallerId()), ct);
        return NoContent();
    }

    [HttpPut("{tid:guid}/config")]
    public async Task<IActionResult> UpdateConfig(
        Guid tid,
        [FromBody] UpdateTenantConfigRequest request,
        CancellationToken ct)
    {
        await _sender.Send(new UpdateTenantConfigCommand(
            tid,
            request.MaxDelegationChainDepth,
            request.PermissionCacheTtlSeconds,
            request.TokenVersionCacheTtlSeconds,
            request.MaxUsersAllowed,
            request.MaxRolesAllowed,
            GetCallerId()), ct);

        return NoContent();
    }

    private Guid GetCallerId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public sealed record CreateTenantRequest(
    string Name, string Slug, string AdminEmail, string AdminPassword);

public sealed record SuspendTenantRequest(string Reason);

public sealed record UpdateTenantConfigRequest(
    int MaxDelegationChainDepth,
    int PermissionCacheTtlSeconds,
    int TokenVersionCacheTtlSeconds,
    int MaxUsersAllowed,
    int MaxRolesAllowed);
