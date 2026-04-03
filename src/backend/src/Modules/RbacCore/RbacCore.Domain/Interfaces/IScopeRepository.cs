using RbacCore.Domain.Entities;

namespace RbacCore.Domain.Interfaces;

public interface IScopeRepository
{
    Task<Scope?> GetByIdAsync(Guid scopeId, CancellationToken ct = default);
    Task<IReadOnlyList<Scope>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Scope>> GetAncestorsAsync(Guid scopeId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(Guid scopeId, Guid tenantId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid scopeId, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Scope scope, CancellationToken ct = default);
    Task AddHierarchyRowsAsync(IEnumerable<ScopeHierarchy> rows, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
