namespace PermissionEngine.Domain.Models;

public sealed class AccessResult
{
    public bool IsGranted { get; }
    public DenialReason? Reason { get; }
    public string? MatchedPolicyId { get; }
    public DelegationChainInfo? DelegationChain { get; }
    public bool CacheHit { get; }
    public long EvaluationLatencyMs { get; }
    public string? DiagnosticMessage { get; }

    private AccessResult(
        bool isGranted,
        DenialReason? reason,
        string? matchedPolicyId,
        DelegationChainInfo? delegationChain,
        bool cacheHit,
        long evaluationLatencyMs,
        string? diagnosticMessage)
    {
        IsGranted = isGranted;
        Reason = reason;
        MatchedPolicyId = matchedPolicyId;
        DelegationChain = delegationChain;
        CacheHit = cacheHit;
        EvaluationLatencyMs = evaluationLatencyMs;
        DiagnosticMessage = diagnosticMessage;
    }

    public static AccessResult Granted(
        bool cacheHit,
        long latencyMs,
        DelegationChainInfo? delegationChain = null,
        string? matchedPolicyId = null)
        => new(true, null, matchedPolicyId, delegationChain, cacheHit, latencyMs, null);

    public static AccessResult Denied(
        DenialReason reason,
        long latencyMs,
        string? diagnosticMessage = null,
        string? matchedPolicyId = null)
        => new(false, reason, matchedPolicyId, null, false, latencyMs, diagnosticMessage);

    public static AccessResult DeniedFromCache(DenialReason reason)
        => new(false, reason, null, null, true, 0, null);
}
