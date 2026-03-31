using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RbacCore.Application.Commands;
using RbacCore.Application.Common;

namespace RbacSystem.Api.Controllers;

/// <summary>
/// Permission catalogue — create named permissions that can be assigned to roles.
/// A permission is the atomic unit of access: it represents one action on one resource type
/// (e.g. <c>users:read</c>, <c>reports:export</c>).
/// </summary>
[ApiController]
[Route("api/v1/tenants/{tid:guid}/permissions")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionsController : ControllerBase
{
    private readonly ISender _sender;

    public PermissionsController(ISender sender) => _sender = sender;

    /// <summary>Create a new permission in the tenant's catalogue.</summary>
    /// <remarks>
    /// Permission codes must be unique per tenant (case-insensitive).
    /// Convention: <c>{resourceType}:{action}</c> (e.g. <c>roles:delete</c>, <c>documents:read</c>).
    /// Once created, a permission is assigned to roles via
    /// <c>POST /roles/{rid}/permissions/{code}</c>.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="request">Permission code, resource type, action, and optional description.</param>
    /// <response code="201">Permission created. Returns the new permission.</response>
    /// <response code="400">Validation failed or a permission with that code already exists.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePermission(
        Guid tid,
        [FromBody] CreatePermissionRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new CreatePermissionCommand(
            tid, request.Code, request.ResourceType, request.Action,
            request.Description, GetCallerId()), ct);

        return Created($"api/v1/tenants/{tid}/permissions/{result.Id}", result);
    }

    private Guid GetCallerId()
    {
        var claim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

/// <summary>Permission creation request.</summary>
public sealed record CreatePermissionRequest(
    /// <summary>
    /// Unique permission code within the tenant. Convention: <c>{resourceType}:{action}</c>,
    /// e.g. <c>roles:delete</c> or <c>documents:export</c>.
    /// </summary>
    string Code,
    /// <summary>Resource type this permission targets (e.g. <c>roles</c>, <c>documents</c>).</summary>
    string ResourceType,
    /// <summary>Action this permission represents (e.g. <c>read</c>, <c>delete</c>, <c>export</c>).</summary>
    string Action,
    /// <summary>Optional human-readable description of what this permission allows.</summary>
    string? Description);
