using PermissionEngine.Domain.Models;

namespace PermissionEngine.Domain.Interfaces;

public interface IPermissionEngine
{
    Task<AccessResult> CanUserAccessAsync(
        Guid userId,
        string action,
        Guid resourceId,
        Guid scopeId,
        EvaluationContext context,
        CancellationToken ct = default);
}
