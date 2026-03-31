using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantManagement.Application.Commands;
using TenantManagement.Application.Common;
using TenantManagement.Application.Queries;
using TenantManagement.Application.Services;

namespace RbacSystem.Api.Controllers;

/// <summary>
/// Tenant lifecycle management — create, read, update, suspend, and delete tenants.
/// Create, suspend, rename, and delete operations require the <c>platform:super-admin</c> role.
/// </summary>
[ApiController]
[Route("api/v1/tenants")]
[Authorize]
[Produces("application/json")]
public sealed class TenantsController : ControllerBase
{
    private readonly ISender _sender;

    public TenantsController(ISender sender) => _sender = sender;

    /// <summary>Create a new tenant and bootstrap its default admin user and roles.</summary>
    /// <remarks>
    /// Runs a <c>TenantBootstrapper</c> atomically: seeds the default admin user,
    /// <c>tenant-admin</c> role with full permissions, and default permission templates
    /// in a single transaction before publishing <c>TenantCreated</c>.
    /// </remarks>
    /// <param name="request">Tenant name, unique URL slug, and initial admin credentials.</param>
    /// <response code="201">Tenant created and bootstrapped. Returns the new tenant.</response>
    /// <response code="400">Validation failed (empty name, duplicate slug, weak password).</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Caller does not hold the <c>platform:super-admin</c> role.</response>
    [HttpPost]
    [Authorize(Roles = "platform:super-admin")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateTenant(
        [FromBody] CreateTenantRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new CreateTenantCommand(
            request.Name, request.Slug, request.AdminEmail, request.AdminPassword,
            GetCallerId()), ct);

        return CreatedAtAction(nameof(GetTenant), new { tid = result.Id }, result);
    }

    /// <summary>Retrieve a tenant by its ID.</summary>
    /// <param name="tid">Tenant UUID.</param>
    /// <response code="200">Tenant found. Returns tenant details and current configuration.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="404">No tenant with the given ID exists (or it is soft-deleted).</response>
    [HttpGet("{tid:guid}")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenant(Guid tid, CancellationToken ct)
    {
        var result = await _sender.Send(new GetTenantByIdQuery(tid), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Rename a tenant.</summary>
    /// <remarks>Only the tenant display name can be changed. The slug is immutable after creation.</remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="request">New tenant name.</param>
    /// <response code="200">Tenant updated. Returns the updated tenant.</response>
    /// <response code="400">Validation failed (empty name, name too long).</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Caller does not hold the <c>platform:super-admin</c> role.</response>
    /// <response code="404">Tenant not found.</response>
    [HttpPut("{tid:guid}")]
    [Authorize(Roles = "platform:super-admin")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTenant(
        Guid tid,
        [FromBody] UpdateTenantRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new UpdateTenantCommand(tid, request.Name, GetCallerId()), ct);
        return Ok(result);
    }

    /// <summary>Soft-delete a tenant.</summary>
    /// <remarks>
    /// Marks the tenant as deleted (<c>IsDeleted = true</c>). All tenant-scoped data
    /// remains in the database for audit purposes and is excluded from queries by
    /// the global EF Core filter. This operation is irreversible via the API.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <response code="204">Tenant soft-deleted successfully.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Caller does not hold the <c>platform:super-admin</c> role.</response>
    /// <response code="404">Tenant not found.</response>
    [HttpDelete("{tid:guid}")]
    [Authorize(Roles = "platform:super-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTenant(Guid tid, CancellationToken ct)
    {
        await _sender.Send(new DeleteTenantCommand(tid, GetCallerId()), ct);
        return NoContent();
    }

    /// <summary>Suspend a tenant, immediately blocking all its users from authenticating.</summary>
    /// <remarks>
    /// Sets <c>IsActive = false</c> and records the suspension reason.
    /// Emits <c>TenantSuspended</c> which busts all cache keys for the tenant.
    /// Use <c>POST /tenants/{tid}/reactivate</c> to lift the suspension (not yet exposed via API).
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="request">Human-readable suspension reason (stored in audit log).</param>
    /// <response code="204">Tenant suspended.</response>
    /// <response code="400">Validation failed or tenant is already suspended.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Caller does not hold the <c>platform:super-admin</c> role.</response>
    /// <response code="404">Tenant not found.</response>
    [HttpPost("{tid:guid}/suspend")]
    [Authorize(Roles = "platform:super-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendTenant(
        Guid tid,
        [FromBody] SuspendTenantRequest request,
        CancellationToken ct)
    {
        await _sender.Send(new SuspendTenantCommand(tid, request.Reason, GetCallerId()), ct);
        return NoContent();
    }

    /// <summary>Update tenant runtime configuration.</summary>
    /// <remarks>
    /// Adjustable settings: max delegation chain depth (1–3), permission/token-version cache TTLs,
    /// and user/role count limits. Changes take effect on the next cache miss.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="request">New configuration values. All fields are required.</param>
    /// <response code="204">Configuration updated successfully.</response>
    /// <response code="400">Validation failed (e.g. chain depth &gt; 3).</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="404">Tenant not found.</response>
    [HttpPut("{tid:guid}/config")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
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
        var claim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

/// <summary>Tenant creation request.</summary>
public sealed record CreateTenantRequest(
    /// <summary>Display name of the tenant (max 200 chars).</summary>
    string Name,
    /// <summary>Unique URL-safe slug (e.g. <c>acme-corp</c>). Immutable after creation.</summary>
    string Slug,
    /// <summary>Email address for the auto-created tenant admin user.</summary>
    string AdminEmail,
    /// <summary>Initial password for the tenant admin user.</summary>
    string AdminPassword);

/// <summary>Tenant suspension request.</summary>
public sealed record SuspendTenantRequest(
    /// <summary>Human-readable reason for suspension, recorded in the audit log.</summary>
    string Reason);

/// <summary>Tenant rename request.</summary>
public sealed record UpdateTenantRequest(
    /// <summary>New display name for the tenant (max 200 chars).</summary>
    string Name);

/// <summary>Tenant configuration update request.</summary>
public sealed record UpdateTenantConfigRequest(
    /// <summary>Maximum allowed delegation chain depth for this tenant (1–3, platform hard limit is 3).</summary>
    int MaxDelegationChainDepth,
    /// <summary>TTL in seconds for the <c>perm:{tid}:{uid}:*</c> Redis cache keys.</summary>
    int PermissionCacheTtlSeconds,
    /// <summary>TTL in seconds for the <c>token-version:{uid}</c> Redis keys (must cover refresh token lifetime).</summary>
    int TokenVersionCacheTtlSeconds,
    /// <summary>Maximum number of active users allowed in this tenant.</summary>
    int MaxUsersAllowed,
    /// <summary>Maximum number of roles allowed in this tenant.</summary>
    int MaxRolesAllowed);
