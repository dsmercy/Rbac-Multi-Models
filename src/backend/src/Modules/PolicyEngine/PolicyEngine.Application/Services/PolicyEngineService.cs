using Microsoft.Extensions.Logging;
using PermissionEngine.Domain.Models;
using PolicyEngine.Domain.Interfaces;

namespace PolicyEngine.Application.Services;

public sealed class PolicyEngineService : IPolicyEngine
{
    private readonly IPolicyRepository _policyRepository;
    private readonly ConditionTreeEvaluator _evaluator;
    private readonly ILogger<PolicyEngineService> _logger;

    public PolicyEngineService(
        IPolicyRepository policyRepository,
        ConditionTreeEvaluator evaluator,
        ILogger<PolicyEngineService> logger)
    {
        _policyRepository = policyRepository;
        _evaluator        = evaluator;
        _logger           = logger;
    }

    public async Task<PolicyEvalResult> EvaluateGlobalPoliciesAsync(
        Guid userId, string action, Guid tenantId,
        EvaluationContext context, CancellationToken ct = default)
    {
        var policies = await _policyRepository.GetGlobalPoliciesAsync(tenantId, ct);
        return EvaluateSet(policies.Where(p => p.IsActive && MatchesAction(p.Action, action)), context);
    }

    public async Task<PolicyEvalResult> EvaluateResourcePoliciesAsync(
        Guid userId, string action, Guid resourceId, Guid tenantId,
        EvaluationContext context, CancellationToken ct = default)
    {
        var policies = await _policyRepository.GetByResourceAsync(resourceId, tenantId, ct);
        return EvaluateSet(policies.Where(p => p.IsActive && MatchesAction(p.Action, action)), context);
    }

    public async Task<PolicyEvalResult> EvaluatePoliciesAsync(
        Guid userId, string action, Guid resourceId, Guid tenantId,
        EvaluationContext context, CancellationToken ct = default)
    {
        var allPolicies = await _policyRepository.GetActivePoliciesAsync(tenantId, ct);

        var applicable = allPolicies
            .Where(p => p.IsActive && MatchesAction(p.Action, action))
            .ToList();

        return EvaluateSet(applicable, context);
    }

    /// <summary>
    /// Evaluates a set of policies against the current evaluation context.
    ///
    /// Error handling (Phase 5 spec):
    ///   • Single rule error: skip the errored rule, log a warning, continue evaluation.
    ///   • If more than 50% of applicable policies throw during evaluation:
    ///     fail-closed → return Deny to prevent a policy misconfiguration from
    ///     silently granting access.
    /// </summary>
    private PolicyEvalResult EvaluateSet(
        IEnumerable<Domain.Entities.Policy> policiesEnumerable,
        EvaluationContext context)
    {
        var policies  = policiesEnumerable.ToList();
        int total     = policies.Count;
        int errorCount = 0;

        foreach (var policy in policies)
        {
            bool conditionMet;
            try
            {
                conditionMet = _evaluator.Evaluate(policy.ConditionTreeJson, context);
            }
            catch (Exception ex)
            {
                errorCount++;

                _logger.LogWarning(ex,
                    "Policy condition evaluation error for policy {PolicyId} ({PolicyName}). " +
                    "Skipping rule. ErrorCount={ErrorCount}/{Total}",
                    policy.Id, policy.Name, errorCount, total);

                // Spec: "if > 50% of applicable policies error in one request, fail-closed"
                if (total > 0 && errorCount * 2 > total)
                {
                    _logger.LogError(
                        "More than 50% of applicable policies ({ErrorCount}/{Total}) errored " +
                        "for tenant {TenantId}. Failing closed to prevent unintended access.",
                        errorCount, total, context.TenantId);

                    return new PolicyEvalResult(
                        PolicyDecision.Deny,
                        MatchedPolicyId: null,
                        FailReason: "More than 50% of applicable policies failed to evaluate. Fail-closed.");
                }

                continue;
            }

            if (!conditionMet)
                continue;

            // First matching DENY wins (deny-overrides-allow)
            if (policy.Effect == Domain.Entities.PolicyEffect.Deny)
                return new PolicyEvalResult(
                    PolicyDecision.Deny, policy.Id.ToString(),
                    $"Policy '{policy.Name}' denied access.");

            if (policy.Effect == Domain.Entities.PolicyEffect.Allow)
                return new PolicyEvalResult(
                    PolicyDecision.Allow, policy.Id.ToString(), null);
        }

        return new PolicyEvalResult(PolicyDecision.NotApplicable, null, null);
    }

    private static bool MatchesAction(string? policyAction, string requestedAction)
        => policyAction is null ||
           policyAction.Equals(requestedAction, StringComparison.OrdinalIgnoreCase);
}
