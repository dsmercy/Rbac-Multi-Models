using Microsoft.EntityFrameworkCore;
using RbacCore.Domain.Entities;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Infrastructure.Persistence.Repositories;

public sealed class ScopeRepository : IScopeRepository
{
    private readonly RbacDbContext _context;

    public ScopeRepository(RbacDbContext context) => _context = context;

    public Task<Scope?> GetByIdAsync(Guid scopeId, CancellationToken ct = default)
        => _context.Scopes.FirstOrDefaultAsync(s => s.Id == scopeId, ct);

    public async Task<IReadOnlyList<Scope>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Scopes
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.Type)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Scope>> GetAncestorsAsync(
        Guid scopeId, Guid tenantId, CancellationToken ct = default)
    {
        // Closure table traversal: find all ancestors ordered by depth ascending
        var ancestorIds = await _context.ScopeHierarchies
            .Where(sh => sh.DescendantId == scopeId && sh.Depth > 0)
            .OrderBy(sh => sh.Depth)
            .Select(sh => sh.AncestorId)
            .ToListAsync(ct);

        return await _context.Scopes
            .Where(s => ancestorIds.Contains(s.Id))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(
        Guid scopeId, Guid tenantId, CancellationToken ct = default)
        => await _context.ScopeHierarchies
            .Where(sh => sh.DescendantId == scopeId && sh.TenantId == tenantId && sh.Depth > 0)
            .OrderBy(sh => sh.Depth)
            .Select(sh => sh.AncestorId)
            .ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid scopeId, Guid tenantId, CancellationToken ct = default)
        => _context.Scopes.AnyAsync(s => s.Id == scopeId && s.TenantId == tenantId, ct);

    public async Task AddAsync(Scope scope, CancellationToken ct = default)
        => await _context.Scopes.AddAsync(scope, ct);

    public async Task AddHierarchyRowsAsync(
        IEnumerable<ScopeHierarchy> rows, CancellationToken ct = default)
        => await _context.ScopeHierarchies.AddRangeAsync(rows, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
