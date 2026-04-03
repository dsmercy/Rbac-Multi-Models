using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RbacCore.Application.Common;
using RbacCore.Application.Queries;

namespace RbacSystem.Api.Controllers;

/// <summary>
/// Scope hierarchy — returns the tenant's scope tree for use in pickers and permission checks.
/// </summary>
[ApiController]
[Route("api/v1/tenants/{tid:guid}/scopes")]
[Authorize]
[Produces("application/json")]
public sealed class ScopesController : ControllerBase
{
    private readonly ISender _sender;

    public ScopesController(ISender sender) => _sender = sender;

    /// <summary>List all scopes in the tenant's hierarchy.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ScopeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListScopes(Guid tid, CancellationToken ct)
    {
        var result = await _sender.Send(new ListScopesQuery(tid), ct);
        return Ok(result);
    }
}
