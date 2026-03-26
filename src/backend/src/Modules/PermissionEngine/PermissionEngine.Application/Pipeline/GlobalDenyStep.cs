using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Models;
using PolicyEngine.Application.Services;
using System.Diagnostics;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// Step 1 — Explicit global deny.
/// If any active policy on the tenant returns an unconditional DENY, short-circuit immediately.
/// </summary>
public sealed class GlobalDenyStep : IEvaluationStep
{
    public int Order => 1;

    private readonly IPolicyEngine _policyEngine;

    public GlobalDenyStep(IPolicyEngine policyEngine)
        => _policyEngine = policyEngine;

    public async Task<AccessResult?> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken ct)
    {
        var result = await _policyEngine.EvaluateGlobalPoliciesAsync(
            request.UserId,
            request.Action,
            request.Context.TenantId,
            request.Context,
            ct);

        if (result.Decision == PolicyDecision.Deny)
        {
            var latency = Stopwatch.GetElapsedTime(request.StartedAt).Milliseconds;
            return AccessResult.Denied(
                DenialReason.ExplicitGlobalDeny,
                latency,
                $"Global policy denied: {result.MatchedPolicyId}",
                result.MatchedPolicyId);
        }

        return null; // Continue pipeline
    }
}
