using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Models;
using System.Diagnostics;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// Step 6 — Role-based permission check.
/// Checks if the user's effective permissions (resolved in step 4) include the
/// requested action. Deny-overrides-allow: a deny role beats an allow role at
/// the same scope level.
/// </summary>
public sealed class RbacPermissionCheckStep : IEvaluationStep
{
    public int Order => 6;

    public Task<AccessResult?> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken ct)
    {
        var latency = Stopwatch.GetElapsedTime(request.StartedAt).Milliseconds;

        var hasPermission = request.EffectivePermissionCodes
            .Any(code => code.Equals(request.Action, StringComparison.OrdinalIgnoreCase));

        if (hasPermission)
        {
            return Task.FromResult<AccessResult?>(
                AccessResult.Granted(
                    cacheHit: false,
                    latencyMs: latency,
                    delegationChain: request.ActiveDelegation));
        }

        // No explicit grant found — fall through to default deny (step 7)
        return Task.FromResult<AccessResult?>(null);
    }
}
