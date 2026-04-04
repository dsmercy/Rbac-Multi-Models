using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RbacCore.Application.Commands;
using RbacCore.Application.Common;
using RbacCore.Application.Queries;

namespace RbacSystem.Api.Controllers;

/// <summary>
/// Role management — create, read, update, delete roles and manage their permission assignments.
/// All endpoints are tenant-scoped: <c>{tid}</c> must match the caller's JWT <c>tid</c> claim.
/// </summary>
[ApiController]
[Route("api/v1/tenants/{tid:guid}/roles")]
[Authorize]
[Produces("application/json")]
public sealed class RolesController : ControllerBase
{
    private readonly ISender _sender;

    public RolesController(ISender sender) => _sender = sender;

    /// <summary>List all non-deleted roles for the tenant.</summary>
    /// <param name="tid">Tenant UUID.</param>
    /// <response code="200">Returns all roles. Each role includes its permission ID list.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListRoles(Guid tid, CancellationToken ct)
    {
        var result = await _sender.Send(new ListRolesQuery(tid), ct);
        return Ok(result);
    }

    /// <summary>Retrieve a single role with its full permission details.</summary>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="roleId">Role UUID.</param>
    /// <response code="200">Role found. Returns role details with hydrated permissions (code, resourceType, action).</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Role not found.</response>
    [HttpGet("{roleId:guid}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRole(Guid tid, Guid roleId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetRoleByIdQuery(tid, roleId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Update a role's name and description.</summary>
    /// <remarks>System roles (<c>IsSystemRole = true</c>) are immutable and return <c>422</c>.</remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="roleId">Role UUID.</param>
    /// <param name="request">New name and optional description.</param>
    /// <response code="200">Role updated. Returns the updated role.</response>
    /// <response code="400">Validation failed or a role with the new name already exists.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Role not found.</response>
    /// <response code="422">System role is immutable (domain invariant).</response>
    [HttpPut("{roleId:guid}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateRole(
        Guid tid,
        Guid roleId,
        [FromBody] UpdateRoleRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new UpdateRoleCommand(tid, roleId, request.Name, request.Description, GetCallerId()), ct);
        return Ok(result);
    }

    /// <summary>Create a new role in the tenant.</summary>
    /// <remarks>Role names must be unique per tenant (case-insensitive). Max 100 chars.</remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="request">Role name and optional description.</param>
    /// <response code="201">Role created. Returns the new role.</response>
    /// <response code="400">Validation failed or a role with that name already exists.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRole(
        Guid tid,
        [FromBody] CreateRoleRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new CreateRoleCommand(tid, request.Name, request.Description, GetCallerId()), ct);

        return Created($"api/v1/tenants/{tid}/roles/{result.Id}", result);
    }

    /// <summary>Soft-delete a role.</summary>
    /// <remarks>
    /// Sets <c>IsDeleted = true</c> on the role and cascades: all active
    /// <c>UserRoleAssignments</c> for this role are deactivated with
    /// <c>DeactivatedReason = "RoleDeleted"</c>. Emits <c>RoleDeleted</c>,
    /// which busts <c>perm:{tid}:*</c> and <c>roles:{tid}:*</c> cache keys.
    /// System roles cannot be deleted (<c>422</c>).
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="roleId">Role UUID.</param>
    /// <response code="204">Role soft-deleted and assignments deactivated.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Role not found.</response>
    /// <response code="422">Cannot delete a system role.</response>
    [HttpDelete("{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteRole(
        Guid tid, Guid roleId, CancellationToken ct)
    {
        await _sender.Send(new DeleteRoleCommand(tid, roleId, GetCallerId()), ct);
        return NoContent();
    }

    /// <summary>List all permissions currently granted to a role.</summary>
    /// <remarks>Returns fully hydrated <c>PermissionDto</c> objects (code, resourceType, action, description).</remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="roleId">Role UUID.</param>
    /// <response code="200">Returns the list of permissions (may be empty).</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch or role belongs to a different tenant.</response>
    /// <response code="404">Role not found.</response>
    [HttpGet("{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRolePermissions(Guid tid, Guid roleId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetRolePermissionsQuery(tid, roleId), ct);
        return Ok(result);
    }

    /// <summary>Grant a permission to a role by permission code.</summary>
    /// <remarks>
    /// Idempotent — granting an already-assigned permission is a no-op (returns <c>204</c>).
    /// Emits <c>PermissionGranted</c> and busts <c>perm:{tid}:*</c> cache.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="roleId">Role UUID.</param>
    /// <param name="permissionCode">Permission code string (e.g. <c>users:read</c>).</param>
    /// <response code="204">Permission granted (or was already granted — idempotent).</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Role or permission not found.</response>
    [HttpPost("{roleId:guid}/permissions/{permissionCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GrantPermission(
        Guid tid, Guid roleId, string permissionCode, CancellationToken ct)
    {
        await _sender.Send(
            new GrantPermissionToRoleCommand(tid, roleId, permissionCode, GetCallerId()), ct);
        return NoContent();
    }

    /// <summary>Revoke a permission from a role by permission code.</summary>
    /// <remarks>
    /// Idempotent — revoking a non-assigned permission is a no-op (returns <c>204</c>).
    /// Emits <c>PermissionRevoked</c> and busts <c>perm:{tid}:*</c> cache.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="roleId">Role UUID.</param>
    /// <param name="permissionCode">Permission code string (e.g. <c>users:read</c>).</param>
    /// <response code="204">Permission revoked (or was not assigned — idempotent).</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Role not found.</response>
    [HttpDelete("{roleId:guid}/permissions/{permissionCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokePermission(
        Guid tid, Guid roleId, string permissionCode, CancellationToken ct)
    {
        await _sender.Send(
            new RevokePermissionFromRoleCommand(tid, roleId, permissionCode, GetCallerId()), ct);
        return NoContent();
    }

    /// <summary>List all active members (users) assigned to a role.</summary>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="roleId">Role UUID.</param>
    /// <response code="200">Returns the list of members with user details.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Role not found.</response>
    [HttpGet("{roleId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<RoleMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoleMembers(Guid tid, Guid roleId, CancellationToken ct)
    {
        var result = await _sender.Send(new ListRoleMembersQuery(tid, roleId), ct);
        return Ok(result);
    }

    /// <summary>List active role assignments for a user (legacy route — prefer <c>GET /users/{uid}/roles</c>).</summary>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="userId">User UUID.</param>
    /// <param name="scopeId">Optional scope filter.</param>
    /// <response code="200">Returns the list of active role assignments.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserRoles(
        Guid tid, Guid userId, [FromQuery] Guid? scopeId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetUserRolesQuery(userId, tid, scopeId), ct);
        return Ok(result);
    }

    /// <summary>Assign a role to a user (legacy route — prefer <c>POST /users/{uid}/roles</c>).</summary>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="userId">User UUID.</param>
    /// <param name="roleId">Role UUID.</param>
    /// <param name="request">Optional scope ID and expiry date.</param>
    /// <response code="204">Role assigned.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">User or role not found.</response>
    [HttpPost("users/{userId:guid}/assign/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(
        Guid tid, Guid userId, Guid roleId,
        [FromBody] AssignRoleRequest request,
        CancellationToken ct)
    {
        await _sender.Send(new AssignRoleToUserCommand(
            tid, userId, roleId, request.ScopeId, request.ExpiresAt, GetCallerId()), ct);
        return NoContent();
    }

    /// <summary>Revoke a role from a user (legacy route — prefer <c>DELETE /users/{uid}/roles/{rid}</c>).</summary>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="userId">User UUID.</param>
    /// <param name="roleId">Role UUID.</param>
    /// <param name="scopeId">Optional scope filter.</param>
    /// <response code="204">Role revoked.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Assignment not found.</response>
    [HttpDelete("users/{userId:guid}/revoke/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
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

/// <summary>Role creation request.</summary>
public sealed record CreateRoleRequest(
    /// <summary>Role name, unique within the tenant (max 100 chars).</summary>
    string Name,
    /// <summary>Optional human-readable description.</summary>
    string? Description);

/// <summary>Role update request.</summary>
public sealed record UpdateRoleRequest(
    /// <summary>New role name (max 100 chars). Must be unique within the tenant.</summary>
    string Name,
    /// <summary>New description (nullable — pass <c>null</c> to clear).</summary>
    string? Description);

/// <summary>Role assignment request (legacy route body).</summary>
public sealed record AssignRoleRequest(
    /// <summary>Optional scope UUID to restrict the role assignment to a specific hierarchy node.</summary>
    Guid? ScopeId,
    /// <summary>Optional UTC expiry — the assignment auto-expires after this time.</summary>
    DateTimeOffset? ExpiresAt);
