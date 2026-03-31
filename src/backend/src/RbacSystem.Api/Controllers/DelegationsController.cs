using Delegation.Application.Commands;
using Delegation.Application.Common;
using Delegation.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RbacSystem.Api.Controllers;

/// <summary>
/// Time-bound delegation management — grant a subset of your permissions to another user
/// for a defined window, with configurable chain depth (max 3, default 1 per tenant).
/// </summary>
[ApiController]
[Route("api/v1/tenants/{tid:guid}/delegations")]
[Authorize]
[Produces("application/json")]
public sealed class DelegationsController : ControllerBase
{
    private readonly ISender _sender;

    public DelegationsController(ISender sender) => _sender = sender;

    /// <summary>Retrieve a delegation grant by ID.</summary>
    /// <remarks>Returns the delegation regardless of its current status (active, revoked, or expired).</remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="did">Delegation UUID.</param>
    /// <response code="200">Delegation found. Returns full grant details.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Delegation not found in this tenant.</response>
    [HttpGet("{did:guid}")]
    [ProducesResponseType(typeof(ActiveDelegationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDelegation(Guid tid, Guid did, CancellationToken ct)
    {
        var result = await _sender.Send(new GetDelegationByIdQuery(tid, did), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Create a time-bound delegation grant.</summary>
    /// <remarks>
    /// <para>
    /// The caller (delegator) is inferred from the JWT <c>sub</c> claim.
    /// Only permissions the delegator currently holds can be delegated — validation
    /// happens at evaluation time, not at creation time, so a delegator who later loses
    /// a permission will have that permission become ineffective in active delegations.
    /// </para>
    /// <para>
    /// <b>Chain depth</b>: the delegatee may not re-delegate unless the tenant's
    /// <c>MaxDelegationChainDepth</c> is &gt; 1. Platform hard limit is 3.
    /// </para>
    /// <para>
    /// Emits <c>DelegationCreated</c>, increments the delegatee's <c>token-version</c> in Redis,
    /// and busts <c>delegation:{tid}:{delegateeId}</c> cache.
    /// </para>
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="request">Delegatee, permission codes, scope, and expiry.</param>
    /// <response code="201">Delegation created. Returns <c>{ id }</c>.</response>
    /// <response code="400">Validation failed (missing permissions, self-delegation, past expiry).</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="422">Domain invariant violated (e.g. delegator = delegatee, chain depth exceeded).</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateDelegationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDelegation(
        Guid tid,
        [FromBody] CreateDelegationRequest request,
        CancellationToken ct)
    {
        var callerId = GetCallerId();

        var id = await _sender.Send(new CreateDelegationCommand(
            tid, callerId, request.DelegateeId, request.PermissionCodes,
            request.ScopeId, request.ExpiresAt, callerId), ct);

        return Created($"api/v1/tenants/{tid}/delegations/{id}", new CreateDelegationResponse(id));
    }

    /// <summary>Revoke a delegation before its natural expiry.</summary>
    /// <remarks>
    /// Immediately marks the delegation as revoked. Takes effect on the next permission
    /// evaluation — no in-flight request can use the delegation after this call.
    /// Emits <c>DelegationRevoked</c>, increments the delegatee's token version, and
    /// busts <c>delegation:{tid}:{delegateeId}</c> cache.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="did">Delegation UUID to revoke.</param>
    /// <response code="204">Delegation revoked.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Delegation not found.</response>
    /// <response code="422">Delegation is already revoked.</response>
    [HttpDelete("{did:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
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

/// <summary>Delegation creation request.</summary>
public sealed record CreateDelegationRequest(
    /// <summary>UUID of the user receiving the delegated permissions.</summary>
    Guid DelegateeId,
    /// <summary>
    /// Permission codes to delegate (e.g. <c>["users:read", "reports:export"]</c>).
    /// All codes must be held by the delegator at the time of evaluation.
    /// </summary>
    IReadOnlyList<string> PermissionCodes,
    /// <summary>Scope UUID within which the delegation is valid.</summary>
    Guid ScopeId,
    /// <summary>UTC timestamp when the delegation expires. Must be in the future.</summary>
    DateTimeOffset ExpiresAt);

/// <summary>Delegation creation response.</summary>
public sealed record CreateDelegationResponse(
    /// <summary>UUID of the newly created delegation grant.</summary>
    Guid Id);
