using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RbacCore.Application.Commands;
using RbacCore.Application.Queries;
using System.Security.Claims;

namespace RbacSystem.Api.Controllers;

[ApiController]
[Route("api/v1/tenants/{tid:guid}/roles")]
[Authorize]
public sealed class RolesController : ControllerBase
{
    private readonly ISender _sender;

    public RolesController(ISender sender) => _sender = sender;

    [HttpPost]
    public async Task<IActionResult> CreateRole(
        Guid tid,
        [FromBody] CreateRoleRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new CreateRoleCommand(tid, request.Name, request.Description, GetCallerId()), ct);

        return Created($"api/v1/tenants/{tid}/roles/{result.Id}", result);
    }

    [HttpDelete("{roleId:guid}")]
    public async Task<IActionResult> DeleteRole(
        Guid tid, Guid roleId, CancellationToken ct)
    {
        await _sender.Send(new DeleteRoleCommand(tid, roleId, GetCallerId()), ct);
        return NoContent();
    }

    [HttpPost("{roleId:guid}/permissions/{permissionCode}")]
    public async Task<IActionResult> GrantPermission(
        Guid tid, Guid roleId, string permissionCode, CancellationToken ct)
    {
        await _sender.Send(
            new GrantPermissionToRoleCommand(tid, roleId, permissionCode, GetCallerId()), ct);
        return NoContent();
    }

    [HttpDelete("{roleId:guid}/permissions/{permissionCode}")]
    public async Task<IActionResult> RevokePermission(
        Guid tid, Guid roleId, string permissionCode, CancellationToken ct)
    {
        await _sender.Send(
            new RevokePermissionFromRoleCommand(tid, roleId, permissionCode, GetCallerId()), ct);
        return NoContent();
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<IActionResult> GetUserRoles(
        Guid tid, Guid userId, [FromQuery] Guid? scopeId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetUserRolesQuery(userId, tid, scopeId), ct);
        return Ok(result);
    }

    [HttpPost("users/{userId:guid}/assign/{roleId:guid}")]
    public async Task<IActionResult> AssignRole(
        Guid tid, Guid userId, Guid roleId,
        [FromBody] AssignRoleRequest request,
        CancellationToken ct)
    {
        await _sender.Send(new AssignRoleToUserCommand(
            tid, userId, roleId,
            request.ScopeId,
            request.ExpiresAt,
            GetCallerId()), ct);

        return NoContent();
    }

    [HttpDelete("users/{userId:guid}/revoke/{roleId:guid}")]
    public async Task<IActionResult> RevokeRole(
        Guid tid, Guid userId, Guid roleId,
        [FromQuery] Guid? scopeId,
        CancellationToken ct)
    {
        await _sender.Send(
            new RevokeRoleFromUserCommand(tid, userId, roleId, scopeId, GetCallerId()), ct);
        return NoContent();
    }

    private Guid GetCallerId()
    {
        var claim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public sealed record CreateRoleRequest(string Name, string? Description);
public sealed record AssignRoleRequest(Guid? ScopeId, DateTimeOffset? ExpiresAt);
