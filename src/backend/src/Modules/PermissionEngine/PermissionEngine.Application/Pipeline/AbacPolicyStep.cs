using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Models;
using PolicyEngine.Application.Services;
using System.Diagnostics;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// Step 5 — ABAC policy evaluation.
/// Evaluates all applicable JSON condition tree policies.
/// Any DENY short-circuits. All must ALLOW (or not apply) to continue.
/// </summary>
public sealed class AbacPolicyStep : IEvaluationStep
{
    public int Order => 5;

    private readonly IPolicyEngine _policyEngine;

    public AbacPolicyStep(IPolicyEngine policyEngine)
        => _policyEngine = policyEngine;

    public async Task<AccessResult?> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken ct)
    {
        var result = await _policyEngine.EvaluatePoliciesAsync(
            request.UserId,
            request.Action,
            request.ResourceId,
            request.Context.TenantId,
            request.Context,
            ct);

        if (result.Decision == PolicyDecision.Deny)
        {
            var latency = Stopwatch.GetElapsedTime(request.StartedAt).Milliseconds;
            return AccessResult.Denied(
                DenialReason.AbacConditionFailed,
                latency,
                $"ABAC policy denied: {result.MatchedPolicyId}",
                result.MatchedPolicyId);
        }

        return null; // Continue to RBAC step
    }
}
