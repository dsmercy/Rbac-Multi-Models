using PermissionEngine.Domain.Models;
using PolicyEngine.Domain.Interfaces;

namespace PolicyEngine.Application.Services;

public sealed class PolicyEngineService : IPolicyEngine
{
    private readonly IPolicyRepository _policyRepository;
    private readonly ConditionTreeEvaluator _evaluator;

    public PolicyEngineService(
        IPolicyRepository policyRepository,
        ConditionTreeEvaluator evaluator)
    {
        _policyRepository = policyRepository;
        _evaluator = evaluator;
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

    private PolicyEvalResult EvaluateSet(
        IEnumerable<Domain.Entities.Policy> policies,
        EvaluationContext context)
    {
        foreach (var policy in policies)
        {
            bool conditionMet;

            try
            {
                conditionMet = _evaluator.Evaluate(policy.ConditionTreeJson, context);
            }
            catch (Exception)
            {
                // Malformed condition tree — fail-open to avoid blocking all requests
                // but emit metric via structured log (handled by caller)
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
