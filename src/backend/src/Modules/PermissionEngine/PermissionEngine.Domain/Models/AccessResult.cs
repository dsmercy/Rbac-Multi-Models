namespace PermissionEngine.Domain.Models;

/// <summary>
/// Immutable result of a single CanUserAccess evaluation.
///
/// Phase-3 additions over the baseline:
///   EvaluatedPolicies — every policy inspected during this evaluation (name + decision)
///   EffectiveRoles    — role names that contributed to the final decision
///
/// These fields are populated during pipeline execution and serialised into the
/// Redis cache entry so callers can inspect the reasoning without re-evaluating.
/// </summary>
public sealed class AccessResult
{
    // ?? Core decision ?????????????????????????????????????????????????????????

    public bool IsGranted { get; }
    public DenialReason? Reason { get; }
    public string? MatchedPolicyId { get; }
    public DelegationChainInfo? DelegationChain { get; }
    public bool CacheHit { get; }
    public long EvaluationLatencyMs { get; }
    public string? DiagnosticMessage { get; }

    // ?? Phase-3 additions ?????????????????????????????????????????????????????

    /// <summary>Every policy that was examined, with its individual decision.</summary>
    public IReadOnlyList<EvaluatedPolicy> EvaluatedPolicies { get; }

    /// <summary>
    /// Role names whose permission set was consulted in step 6 of the pipeline.
    /// Populated only on non-cache-hit evaluations.
    /// </summary>
    public IReadOnlyList<string> EffectiveRoles { get; }

    // ?? Private constructor ???????????????????????????????????????????????????

    private AccessResult(
        bool isGranted,
        DenialReason? reason,
        string? matchedPolicyId,
        DelegationChainInfo? delegationChain,
        bool cacheHit,
        long evaluationLatencyMs,
        string? diagnosticMessage,
        IReadOnlyList<EvaluatedPolicy>? evaluatedPolicies,
        IReadOnlyList<string>? effectiveRoles)
    {
        IsGranted = isGranted;
        Reason = reason;
        MatchedPolicyId = matchedPolicyId;
        DelegationChain = delegationChain;
        CacheHit = cacheHit;
        EvaluationLatencyMs = evaluationLatencyMs;
        DiagnosticMessage = diagnosticMessage;
        EvaluatedPolicies = evaluatedPolicies ?? Array.Empty<EvaluatedPolicy>();
        EffectiveRoles = effectiveRoles ?? Array.Empty<string>();
    }

    // ?? Factory methods ???????????????????????????????????????????????????????

    public static AccessResult Granted(
        bool cacheHit,
        long latencyMs,
        DelegationChainInfo? delegationChain = null,
        string? matchedPolicyId = null,
        IReadOnlyList<EvaluatedPolicy>? evaluatedPolicies = null,
        IReadOnlyList<string>? effectiveRoles = null)
        => new(
            true, null, matchedPolicyId, delegationChain,
            cacheHit, latencyMs, null,
            evaluatedPolicies, effectiveRoles);

    public static AccessResult Denied(
        DenialReason reason,
        long latencyMs,
        string? diagnosticMessage = null,
        string? matchedPolicyId = null,
        IReadOnlyList<EvaluatedPolicy>? evaluatedPolicies = null,
        IReadOnlyList<string>? effectiveRoles = null)
        => new(
            false, reason, matchedPolicyId, null,
            false, latencyMs, diagnosticMessage,
            evaluatedPolicies, effectiveRoles);

    /// <summary>Used when returning a previously cached denied result.</summary>
    public static AccessResult DeniedFromCache(DenialReason reason)
        => new(false, reason, null, null, true, 0, null, null, null);

    /// <summary>Used when returning a previously cached granted result.</summary>
    public static AccessResult GrantedFromCache(DelegationChainInfo? delegationChain = null)
        => new(true, null, null, delegationChain, true, 0, null, null, null);
}

// ?? Supporting types ??????????????????????????????????????????????????????????

/// <summary>A single policy's contribution to the evaluation.</summary>
public sealed record EvaluatedPolicy(
    string PolicyId,
    string PolicyName,
    PolicyOutcome Outcome);

public enum PolicyOutcome
{
    NotApplicable,
    Allow,
    Deny
}