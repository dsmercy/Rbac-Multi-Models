using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PolicyEngine.Application.Commands;
using PolicyEngine.Application.Common;
using PolicyEngine.Application.Queries;
using PolicyEngine.Domain.Entities;

namespace RbacSystem.Api.Controllers;

/// <summary>
/// ABAC policy management — create, read, update, and delete JSON condition tree policies.
/// Policies are evaluated in the permission engine pipeline (Step 5) after scope inheritance.
/// An active <c>Deny</c> policy short-circuits the evaluation regardless of role grants.
/// </summary>
[ApiController]
[Route("api/v1/tenants/{tid:guid}/policies")]
[Authorize]
[Produces("application/json")]
public sealed class PoliciesController : ControllerBase
{
    private readonly ISender _sender;

    public PoliciesController(ISender sender) => _sender = sender;

    /// <summary>List all policies in the tenant.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListPolicies(Guid tid, CancellationToken ct)
    {
        var result = await _sender.Send(new ListPoliciesQuery(tid), ct);
        return Ok(result);
    }

    /// <summary>Retrieve a policy by ID.</summary>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="policyId">Policy UUID.</param>
    /// <response code="200">Policy found. Returns full policy including the condition tree JSON.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Policy not found.</response>
    [HttpGet("{policyId:guid}")]
    [ProducesResponseType(typeof(PolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPolicy(Guid tid, Guid policyId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetPolicyByIdQuery(tid, policyId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Update a policy's name, description, condition tree, and action filter.</summary>
    /// <remarks>
    /// The policy <c>Effect</c> (<c>Allow</c>/<c>Deny</c>) is immutable after creation.
    /// To change the effect, delete and recreate the policy.
    /// Emits <c>PolicyUpdated</c> and busts <c>perm:{tid}:*</c> and <c>policy:{tid}:{pid}</c> cache keys.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="policyId">Policy UUID.</param>
    /// <param name="request">Updated name, description, condition tree JSON, and action filter.</param>
    /// <response code="204">Policy updated successfully.</response>
    /// <response code="400">Validation failed or condition tree JSON is malformed.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Policy not found.</response>
    [HttpPut("{policyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePolicy(
        Guid tid,
        Guid policyId,
        [FromBody] UpdatePolicyRequest request,
        CancellationToken ct)
    {
        await _sender.Send(new UpdatePolicyCommand(
            tid, policyId, request.Name, request.Description,
            request.ConditionTreeJson, request.Action, GetCallerId()), ct);
        return NoContent();
    }

    /// <summary>Create a new ABAC policy.</summary>
    /// <remarks>
    /// <para>
    /// <b>Effect</b>: <c>1 = Allow</c>, <c>2 = Deny</c>. A <c>Deny</c> policy short-circuits
    /// the entire evaluation pipeline when matched.
    /// </para>
    /// <para>
    /// <b>ConditionTreeJson</b>: serialised JSON string representing the condition tree.
    /// Example: <c>{"op":"AND","conditions":[{"attribute":"user.department","op":"eq","value":"engineering"}]}</c>
    /// </para>
    /// <para>
    /// <b>ResourceId</b>: when set, the policy applies only to that specific resource ID.
    /// When null, the policy applies globally across the tenant.
    /// </para>
    /// <para>
    /// <b>Action</b>: when set, the policy applies only to that action string (e.g. <c>users:delete</c>).
    /// When null, the policy applies to all actions.
    /// </para>
    /// Emits <c>PolicyCreated</c> and busts <c>perm:{tid}:*</c> cache.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="request">Policy definition.</param>
    /// <response code="201">Policy created. Returns <c>{ id }</c>.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreatePolicyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePolicy(
        Guid tid,
        [FromBody] CreatePolicyRequest request,
        CancellationToken ct)
    {
        var id = await _sender.Send(new CreatePolicyCommand(
            tid, request.Name, request.Description, request.Effect,
            request.ConditionTreeJson, request.ResourceId, request.Action,
            GetCallerId()), ct);

        return Created($"api/v1/tenants/{tid}/policies/{id}", new CreatePolicyResponse(id));
    }

    /// <summary>Soft-delete a policy.</summary>
    /// <remarks>
    /// Marks the policy as deleted. It will no longer be evaluated in the permission engine.
    /// Emits <c>PolicyDeleted</c> and busts <c>perm:{tid}:*</c> and <c>policy:{tid}:{pid}</c> cache keys.
    /// </remarks>
    /// <param name="tid">Tenant UUID.</param>
    /// <param name="policyId">Policy UUID.</param>
    /// <response code="204">Policy soft-deleted.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Tenant ID mismatch.</response>
    /// <response code="404">Policy not found.</response>
    [HttpDelete("{policyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
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

/// <summary>Policy creation request.</summary>
public sealed record CreatePolicyRequest(
    /// <summary>Policy display name (unique within tenant).</summary>
    string Name,
    /// <summary>Optional description of what the policy enforces.</summary>
    string? Description,
    /// <summary>Policy effect: <c>1 = Allow</c>, <c>2 = Deny</c>. Immutable after creation.</summary>
    PolicyEffect Effect,
    /// <summary>
    /// Serialised JSON condition tree evaluated by the ABAC engine.
    /// Must be a valid JSON string (not a nested object in the request body).
    /// </summary>
    string ConditionTreeJson,
    /// <summary>Optional resource UUID to scope this policy to a single resource. Null = applies globally.</summary>
    Guid? ResourceId,
    /// <summary>Optional action string to scope this policy (e.g. <c>users:delete</c>). Null = applies to all actions.</summary>
    string? Action);

/// <summary>Policy update request.</summary>
public sealed record UpdatePolicyRequest(
    /// <summary>New policy name.</summary>
    string Name,
    /// <summary>New description (nullable — pass <c>null</c> to clear).</summary>
    string? Description,
    /// <summary>Updated condition tree JSON.</summary>
    string ConditionTreeJson,
    /// <summary>Updated action filter (nullable — pass <c>null</c> to match all actions).</summary>
    string? Action);

/// <summary>Policy creation response.</summary>
public sealed record CreatePolicyResponse(
    /// <summary>UUID of the newly created policy.</summary>
    Guid Id);
