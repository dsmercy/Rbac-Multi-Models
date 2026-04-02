using Microsoft.EntityFrameworkCore;
using PolicyEngine.Domain.Entities;
using PolicyEngine.Domain.Interfaces;

namespace PolicyEngine.Infrastructure.Persistence.Repositories;

public sealed class PolicyRepository : IPolicyRepository
{
    private readonly PolicyDbContext _context;

    public PolicyRepository(PolicyDbContext context) => _context = context;

    public Task<Policy?> GetByIdAsync(Guid policyId, CancellationToken ct = default)
        => _context.Policies.FirstOrDefaultAsync(p => p.Id == policyId, ct);

    public async Task<IReadOnlyList<Policy>> GetActivePoliciesAsync(
        Guid tenantId, CancellationToken ct = default)
        => await _context.Policies
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Policy>> GetAllByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
        => await _context.Policies
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Policy>> GetByResourceAsync(
        Guid resourceId, Guid tenantId, CancellationToken ct = default)
        => await _context.Policies
            .Where(p => p.TenantId == tenantId && p.ResourceId == resourceId && p.IsActive)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Policy>> GetGlobalPoliciesAsync(
        Guid tenantId, CancellationToken ct = default)
        => await _context.Policies
            .Where(p => p.TenantId == tenantId && p.ResourceId == null && p.IsActive)
            .ToListAsync(ct);

    public async Task AddAsync(Policy policy, CancellationToken ct = default)
        => await _context.Policies.AddAsync(policy, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
