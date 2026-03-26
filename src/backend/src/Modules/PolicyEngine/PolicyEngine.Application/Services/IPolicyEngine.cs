using PermissionEngine.Domain.Models;

namespace PolicyEngine.Application.Services;

/// <summary>
/// Public anti-corruption interface consumed by PermissionEngine pipeline steps.
/// </summary>
public interface IPolicyEngine
{
    Task<PolicyEvalResult> EvaluateGlobalPoliciesAsync(
        Guid userId,
        string action,
        Guid tenantId,
        EvaluationContext context,
        CancellationToken ct = default);

    Task<PolicyEvalResult> EvaluateResourcePoliciesAsync(
        Guid userId,
        string action,
        Guid resourceId,
        Guid tenantId,
        EvaluationContext context,
        CancellationToken ct = default);

    Task<PolicyEvalResult> EvaluatePoliciesAsync(
        Guid userId,
        string action,
        Guid resourceId,
        Guid tenantId,
        EvaluationContext context,
        CancellationToken ct = default);
}
