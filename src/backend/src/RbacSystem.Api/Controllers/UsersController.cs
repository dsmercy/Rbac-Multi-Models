using BuildingBlocks.Application;
using Identity.Application.Commands;
using Identity.Application.Common;
using Identity.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RbacCore.Application.Commands;
using RbacCore.Application.Common;
using RbacCore.Application.Queries;

namespace RbacSystem.Api.Controllers;

/// <summary>
/// User management within a tenant — create, read, update users and manage their role assignments.
/// All endpoints are tenant-scoped: the <c>{tid}</c> in the route must match the <c>tid</c> JWT claim.
/// </summary>
[ApiController]
[Route("api/v1/tenants/{tid:guid}/users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender) => _sender = sender;

    /// <summary>List users in the tenant with optional search and pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListUsers(
        Guid tid,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new ListUsersQuery(tid, search, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Create a new user in the tenant.</summary>
    /// <remarks>
    /// Hashes the password using PBKDF2-SHA512 (310,000 iterations) before storage.
    /// Emits <c>UserCreated</c>. Returns <c>400</c> if the email already exists in the tenant.
    /// </remarks>
    /// <param name="tid">Tenant UUID (must match the caller's <c>tid</c> JWT claim).</param>
    /// <param name="request">Email, display name, and initial password.</param>
    /// <response code="201">User created. Returns the new user profile.</response>
    /// <response code="400">Validation failed or email already exists in this tenant.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch — caller's JWT <c>tid</c> differs from route <c>{tid}</c>.</response>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateUser(
        Guid tid,
        [FromBody] CreateUserRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new CreateUserCommand(tid, request.Email, request.DisplayName, request.Password, GetCallerId()), ct);

        return Created($"api/v1/tenants/{tid}/users/{result.Id}", result);
    }

    /// <summary>Retrieve a user by ID.</summary>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="uid">User UUID.</param>
    /// <response code="200">User found. Returns the user profile.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">User not found in this tenant.</response>
    [HttpGet("{uid:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid tid, Guid uid, CancellationToken ct)
    {
        var result = await _sender.Send(new GetUserByIdQuery(uid, tid), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Update a user's display name.</summary>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="uid">User UUID.</param>
    /// <param name="request">New display name (max 150 chars).</param>
    /// <response code="200">User updated. Returns the updated user profile.</response>
    /// <response code="400">Validation failed (empty or too-long display name).</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("{uid:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(
        Guid tid,
        Guid uid,
        [FromBody] UpdateUserRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new UpdateUserCommand(tid, uid, request.DisplayName, GetCallerId()), ct);
        return Ok(result);
    }

    /// <summary>Assign a role to a user within an optional scope.</summary>
    /// <remarks>
    /// Privilege-escalation prevention: the caller may not assign a role that grants
    /// more permissions than the caller themselves holds. Emits <c>UserRoleAssigned</c>,
    /// which increments the user's <c>token-version</c> in Redis, invalidating any
    /// in-flight access token for that user.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="uid">User UUID to assign the role to.</param>
    /// <param name="request">Role ID, optional scope ID, and optional expiry date.</param>
    /// <response code="204">Role assigned successfully.</response>
    /// <response code="400">Validation failed or role not found in this tenant.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch, or caller lacks sufficient permissions to assign this role.</response>
    /// <response code="404">User or role not found.</response>
    [HttpPost("{uid:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(
        Guid tid,
        Guid uid,
        [FromBody] AssignRoleToUserRequest request,
        CancellationToken ct)
    {
        await _sender.Send(new AssignRoleToUserCommand(
            tid, uid, request.RoleId, request.ScopeId, request.ExpiresAt, GetCallerId()), ct);
        return NoContent();
    }

    /// <summary>Revoke a role from a user.</summary>
    /// <remarks>
    /// Marks the <c>UserRoleAssignment</c> as inactive. Emits <c>UserRoleRevoked</c>,
    /// which increments the user's token version and busts permission cache entries.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="uid">User UUID.</param>
    /// <param name="rid">Role UUID to revoke.</param>
    /// <param name="scopeId">
    /// Optional scope UUID. When provided, only the assignment at that specific scope is revoked.
    /// When omitted, the first active assignment for the role is revoked.
    /// </param>
    /// <response code="204">Role revoked successfully.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Assignment not found.</response>
    [HttpDelete("{uid:guid}/roles/{rid:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeRole(
        Guid tid,
        Guid uid,
        Guid rid,
        [FromQuery] Guid? scopeId,
        CancellationToken ct)
    {
        await _sender.Send(new RevokeRoleFromUserCommand(tid, uid, rid, scopeId, GetCallerId()), ct);
        return NoContent();
    }

    /// <summary>List all active role assignments for a user, optionally filtered by scope.</summary>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="uid">User UUID.</param>
    /// <param name="scopeId">
    /// Optional scope filter. When provided, only assignments at that exact scope are returned.
    /// Omit to return assignments across all scopes.
    /// </param>
    /// <response code="200">Returns the list of active role assignments (may be empty).</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    [HttpGet("{uid:guid}/roles")]
    [ProducesResponseType(typeof(IReadOnlyList<UserRoleAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserRoles(
        Guid tid,
        Guid uid,
        [FromQuery] Guid? scopeId,
        CancellationToken ct)
    {
        var result = await _sender.Send(new GetUserRolesQuery(uid, tid, scopeId), ct);
        return Ok(result);
    }

    /// <summary>Mark onboarding as completed for a user.</summary>
    /// <remarks>
    /// Called by the frontend when the setup wizard is completed or dismissed.
    /// This is a best-effort call — the frontend persists completion state locally
    /// and uses this endpoint to sync with the backend for audit purposes.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="uid">User UUID.</param>
    /// <response code="204">Onboarding marked complete.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    [HttpPatch("{uid:guid}/onboarding")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public IActionResult CompleteOnboarding(Guid tid, Guid uid)
    {
        // Onboarding completion is currently tracked client-side.
        // This endpoint exists for future server-side persistence and audit logging.
        return NoContent();
    }

    private Guid GetCallerId()
    {
        var claim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

/// <summary>User creation request.</summary>
public sealed record CreateUserRequest(
    /// <summary>User's email address. Must be unique within the tenant.</summary>
    string Email,
    /// <summary>Display name shown in the admin panel (max 150 chars).</summary>
    string DisplayName,
    /// <summary>Initial plaintext password. Stored as PBKDF2-SHA512 hash.</summary>
    string Password);

/// <summary>User profile update request.</summary>
public sealed record UpdateUserRequest(
    /// <summary>New display name (max 150 chars).</summary>
    string DisplayName);

/// <summary>Role assignment request.</summary>
public sealed record AssignRoleToUserRequest(
    /// <summary>UUID of the role to assign.</summary>
    Guid RoleId,
    /// <summary>Optional scope UUID. When provided, the role is scoped to that node in the hierarchy.</summary>
    Guid? ScopeId,
    /// <summary>Optional expiry date. When provided, the assignment auto-expires at this time (UTC).</summary>
    DateTimeOffset? ExpiresAt);
