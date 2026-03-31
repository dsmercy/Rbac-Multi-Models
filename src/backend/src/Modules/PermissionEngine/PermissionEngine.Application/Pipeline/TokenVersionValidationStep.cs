using PermissionEngine.Domain.Exceptions;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using System.Diagnostics;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// Step 0 — Token version validation.
///
/// Phase 4 spec: "check a Redis key token-version:{userId} against the tv claim
/// in the token. If the token version is stale, reject with 401 and force
/// re-authentication."
///
/// This step runs BEFORE every pipeline evaluation, even before global deny.
/// A stale token means a role or delegation changed after this JWT was issued,
/// so the embedded role list cannot be trusted.
///
/// Behaviour:
///   • TokenVersion == null   → skip (server-to-server call, no JWT)
///   • version matches        → continue pipeline (return null)
///   • version mismatch       → throw StaleTokenException (→ 401 via GlobalExceptionMiddleware)
///
/// Note: we THROW rather than return AccessResult.Denied so the HTTP response
/// is 401 Unauthorized (force re-auth), not 403 Forbidden (permission denied).
/// These are semantically distinct: 403 means "you don't have access", 401 means
/// "your session is outdated, get a fresh token first."
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
        // Skip for server-to-server calls that don't carry a user JWT.
        if (request.Context.TokenVersion is null)
            return null;

        var currentVersion = await _cacheService.GetTokenVersionAsync(request.UserId, ct);

        if (request.Context.TokenVersion.Value != currentVersion)
        {
            // Throw so GlobalExceptionMiddleware maps this to HTTP 401.
            // The client must use the refresh token to get a new access token
            // that carries the updated version and role list.
            throw new StaleTokenException(
                $"Token version {request.Context.TokenVersion.Value} is stale. " +
                $"Current version is {currentVersion}. Please re-authenticate.");
        }

        return null; // Version is current — continue pipeline
    }
}
