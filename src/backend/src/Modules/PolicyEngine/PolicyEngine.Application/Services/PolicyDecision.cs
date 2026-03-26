namespace PolicyEngine.Application.Services;

public enum PolicyDecision
{
    NotApplicable,
    Allow,
    Deny
}

public sealed record PolicyEvalResult(
    PolicyDecision Decision,
    string? MatchedPolicyId,
    string? FailReason);
