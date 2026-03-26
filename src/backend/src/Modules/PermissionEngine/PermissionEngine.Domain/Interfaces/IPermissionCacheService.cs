using PermissionEngine.Domain.Models;

namespace PermissionEngine.Domain.Interfaces;

public interface IPermissionCacheService
{
    Task<AccessResult?> GetAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId,
        CancellationToken ct = default);

    Task SetAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId, Guid tenantId,
        AccessResult result, TimeSpan ttl,
        CancellationToken ct = default);

    Task InvalidateUserAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    Task<int> GetTokenVersionAsync(Guid userId, CancellationToken ct = default);

    Task IncrementTokenVersionAsync(Guid userId, CancellationToken ct = default);
}
