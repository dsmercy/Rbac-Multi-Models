using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Models;
using System.Diagnostics;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// Step 7 — Default deny.
/// If no earlier step granted access, deny by default.
/// This step always produces a result — it never returns null.
/// </summary>
public sealed class DefaultDenyStep : IEvaluationStep
{
    public int Order => 7;

    public Task<AccessResult?> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken ct)
    {
        var latency = Stopwatch.GetElapsedTime(request.StartedAt).Milliseconds;

        return Task.FromResult<AccessResult?>(
            AccessResult.Denied(
                DenialReason.NoPermissionFound,
                latency,
                $"No permission found for action '{request.Action}' after full pipeline evaluation."));
    }
}
