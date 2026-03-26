using Microsoft.EntityFrameworkCore;
using RbacCore.Domain.Entities;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Infrastructure.Persistence.Repositories;

public sealed class PermissionRepository : IPermissionRepository
{
    private readonly RbacDbContext _context;

    public PermissionRepository(RbacDbContext context) => _context = context;

    public Task<Permission?> GetByIdAsync(Guid permissionId, CancellationToken ct = default)
        => _context.Permissions.FirstOrDefaultAsync(p => p.Id == permissionId, ct);

    public Task<Permission?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default)
        => _context.Permissions
            .FirstOrDefaultAsync(p => p.Code.Value == code && p.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<Permission>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Permissions
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Permission>> GetByCodesAsync(
        IEnumerable<string> codes, Guid tenantId, CancellationToken ct = default)
    {
        var codeList = codes.ToList();
        return await _context.Permissions
            .Where(p => p.TenantId == tenantId && codeList.Contains(p.Code.Value))
            .ToListAsync(ct);
    }

    public Task<bool> ExistsAsync(Guid permissionId, Guid tenantId, CancellationToken ct = default)
        => _context.Permissions
            .AnyAsync(p => p.Id == permissionId && p.TenantId == tenantId, ct);

    public async Task AddAsync(Permission permission, CancellationToken ct = default)
        => await _context.Permissions.AddAsync(permission, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
