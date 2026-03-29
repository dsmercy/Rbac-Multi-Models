using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using System.Diagnostics;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// Step 0 — Token version validation.
///
/// Before any permission evaluation begins, verify that the calling user's
/// token version in Redis matches the version embedded in the EvaluationContext.
/// If the versions differ it means a role, delegation, or permission was changed
/// after this token was issued — evaluation must be aborted and the caller forced
/// to re-authenticate.
///
/// This step is ORDER = 0 so it always runs before all other pipeline steps.
///
/// Design rationale:
///   Token version is incremented in Redis on: UserRoleAssigned, UserRoleRevoked,
///   DelegationCreated, DelegationRevoked. The version is embedded in the JWT as
///   the "tv" claim and surfaced to the pipeline via EvaluationContext.TokenVersion.
///   If the context carries no version (TokenVersion == null), the check is skipped
///   — this supports internal server-to-server calls that bypass JWT validation.
/// </summary>
public sealed class TokenVersionValidationStep : IEvaluationStep
{
    public int Order => 0;

    private readonly IPermissionCacheService _cacheService;

    public TokenVersionValidationStep(IPermissionCacheService cacheService)
        => _cacheService = cacheService;

    public async Task<AccessResult?> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken ct)
    {
        // Skip for server-to-server calls that don't carry a token version.
        if (request.Context.TokenVersion is null)
            return null;

        var currentVersion = await _cacheService.GetTokenVersionAsync(request.UserId, ct);

        if (request.Context.TokenVersion.Value != currentVersion)
        {
            var latency = Stopwatch.GetElapsedTime(request.StartedAt).Milliseconds;
            return AccessResult.Denied(
                DenialReason.TokenVersionMismatch,
                latency,
                $"Token version {request.Context.TokenVersion} is stale. " +
                $"Current version is {currentVersion}. Re-authentication required.");
        }

        return null; // Version is current — continue pipeline
    }
}