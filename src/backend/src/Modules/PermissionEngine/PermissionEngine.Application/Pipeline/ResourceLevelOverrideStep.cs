using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Models;
using PolicyEngine.Application.Services;
using System.Diagnostics;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// Step 2 — Resource-level override.
/// Checks for a direct DENY or ALLOW policy scoped to the exact ResourceId.
/// A resource-level DENY short-circuits. A resource-level ALLOW is noted but
/// still proceeds to delegation check before final grant.
/// </summary>
public sealed class ResourceLevelOverrideStep : IEvaluationStep
{
    public int Order => 2;

    private readonly IPolicyEngine _policyEngine;

    public ResourceLevelOverrideStep(IPolicyEngine policyEngine)
        => _policyEngine = policyEngine;

    public async Task<AccessResult?> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken ct)
    {
        var result = await _policyEngine.EvaluateResourcePoliciesAsync(
            request.UserId,
            request.Action,
            request.ResourceId,
            request.Context.TenantId,
            request.Context,
            ct);

        var latency = Stopwatch.GetElapsedTime(request.StartedAt).Milliseconds;

        if (result.Decision == PolicyDecision.Deny)
        {
            return AccessResult.Denied(
                DenialReason.ResourceLevelDeny,
                latency,
                $"Resource-level policy denied: {result.MatchedPolicyId}",
                result.MatchedPolicyId);
        }

        // ALLOW at resource level is noted but pipeline continues to delegation check
        return null;
    }
}
