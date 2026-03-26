using Microsoft.EntityFrameworkCore;
using TenantManagement.Domain.Entities;
using TenantManagement.Domain.Interfaces;

namespace TenantManagement.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly TenantDbContext _context;

    public TenantRepository(TenantDbContext context)
        => _context = context;

    public async Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _context.Tenants
            .FirstOrDefaultAsync(t => t.Slug.Value == slug, ct);

    public async Task<bool> ExistsAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Tenants.AnyAsync(t => t.Id == tenantId, ct);

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => await _context.Tenants
            .AnyAsync(t => t.Slug.Value == slug, ct);

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
        => await _context.Tenants.AddAsync(tenant, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
