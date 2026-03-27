using Delegation.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Delegation.Infrastructure.Persistence.Repositories;

public sealed class DelegationRepository : IDelegationRepository
{
    private readonly DelegationDbContext _context;

    public DelegationRepository(DelegationDbContext context) => _context = context;

    public Task<Domain.Entities.DelegationGrant?> GetByIdAsync(
        Guid delegationId, CancellationToken ct = default)
        => _context.Delegations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == delegationId, ct);

    public Task<Domain.Entities.DelegationGrant?> GetActiveDelegationAsync(
        Guid delegateeId, string action, Guid scopeId, Guid tenantId,
        CancellationToken ct = default)
        // EF.Property targets the mapped field name "_permissionCodes" because
        // PermissionCodes is [NotMapped] and cannot be used in LINQ-to-SQL.
        => _context.Delegations
            .FirstOrDefaultAsync(d =>
                d.TenantId == tenantId &&
                d.DelegateeId == delegateeId &&
                d.ScopeId == scopeId &&
                EF.Property<List<string>>(d, "_permissionCodes").Contains(action), ct);

    public async Task<IReadOnlyList<Domain.Entities.DelegationGrant>> GetActiveByDelegateeAsync(
        Guid delegateeId, Guid tenantId, CancellationToken ct = default)
        => await _context.Delegations
            .Where(d => d.TenantId == tenantId && d.DelegateeId == delegateeId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Domain.Entities.DelegationGrant>> GetActiveByDelegatorAsync(
        Guid delegatorId, Guid tenantId, CancellationToken ct = default)
        => await _context.Delegations
            .Where(d => d.TenantId == tenantId && d.DelegatorId == delegatorId)
            .ToListAsync(ct);

    public async Task AddAsync(
        Domain.Entities.DelegationGrant delegation, CancellationToken ct = default)
        => await _context.Delegations.AddAsync(delegation, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
