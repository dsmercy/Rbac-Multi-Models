using AuditLogging.Domain.Entities;
using AuditLogging.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuditLogging.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AuditDbContext _context;

    public AuditLogRepository(AuditDbContext context) => _context = context;

    public async Task AppendAsync(AuditLog entry, CancellationToken ct = default)
    {
        await _context.AuditLogs.AddAsync(entry, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? userId,
        string? action,
        Guid? resourceId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.AuditLogs
            .Where(a => a.TenantId == tenantId &&
                        a.Timestamp >= from &&
                        a.Timestamp <= to);

        if (userId.HasValue)
            query = query.Where(a => a.ActorUserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (resourceId.HasValue)
            query = query.Where(a => a.ResourceId == resourceId.Value);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? userId,
        string? action,
        Guid? resourceId,
        CancellationToken ct = default)
    {
        var query = _context.AuditLogs
            .Where(a => a.TenantId == tenantId &&
                        a.Timestamp >= from &&
                        a.Timestamp <= to);

        if (userId.HasValue)
            query = query.Where(a => a.ActorUserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (resourceId.HasValue)
            query = query.Where(a => a.ResourceId == resourceId.Value);

        return query.CountAsync(ct);
    }
}
