using Microsoft.EntityFrameworkCore;
using RbacCore.Domain.Entities;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Infrastructure.Persistence.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly RbacDbContext _context;

    public RoleRepository(RbacDbContext context) => _context = context;

    public Task<Role?> GetByIdAsync(Guid roleId, CancellationToken ct = default)
        => _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);

    public Task<Role?> GetByIdIgnoreFiltersAsync(Guid roleId, CancellationToken ct = default)
    => _context.Roles
        .IgnoreQueryFilters()
        .Include(r => r.Permissions)
        .FirstOrDefaultAsync(r => r.Id == roleId && !r.IsDeleted, ct);

    public Task<Role?> GetByNameAsync(string name, Guid tenantId, CancellationToken ct = default)
        => _context.Roles
            .FirstOrDefaultAsync(r => r.Name == name && r.TenantId == tenantId, ct);

    public Task<bool> ExistsAsync(Guid roleId, Guid tenantId, CancellationToken ct = default)
        => _context.Roles.AnyAsync(r => r.Id == roleId && r.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<Role>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Roles
            .Include(r => r.Permissions)
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(ct);

    public async Task AddAsync(Role role, CancellationToken ct = default)
        => await _context.Roles.AddAsync(role, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
