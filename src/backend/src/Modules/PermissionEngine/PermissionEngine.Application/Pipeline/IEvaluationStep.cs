using PermissionEngine.Domain.Models;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// One step in the 7-step evaluation pipeline.
/// Each step returns null to continue to the next step,
/// or a final AccessResult to short-circuit the pipeline.
/// </summary>
public interface IEvaluationStep
{
    int Order { get; }

    Task<AccessResult?> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken ct);
}

public sealed class EvaluationRequest
{
    public Guid UserId { get; init; }
    public string Action { get; init; } = null!;
    public Guid ResourceId { get; init; }
    public Guid ScopeId { get; init; }
    public EvaluationContext Context { get; init; } = null!;
    public long StartedAt { get; init; }

    // Resolved during pipeline — written by steps, read by later steps
    public IReadOnlyList<string> EffectivePermissionCodes { get; set; } = [];
    public IReadOnlyList<Guid> ResolvedScopeIds { get; set; } = [];
    public DelegationChainInfo? ActiveDelegation { get; set; }
}
