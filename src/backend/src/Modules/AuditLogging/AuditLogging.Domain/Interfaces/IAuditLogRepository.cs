using AuditLogging.Domain.Entities;

namespace AuditLogging.Domain.Interfaces;

public interface IAuditLogRepository
{
    /// <summary>The only permitted write operation — no Update or Delete.</summary>
    Task AppendAsync(AuditLog entry, CancellationToken ct = default);

    Task<IReadOnlyList<AuditLog>> QueryAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? userId,
        string? action,
        Guid? resourceId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<int> CountAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? userId,
        string? action,
        Guid? resourceId,
        CancellationToken ct = default);
}
