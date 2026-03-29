using RbacCore.Domain.Entities;

namespace RbacCore.Domain.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid roleId, CancellationToken ct = default);
    Task<Role?> GetByIdIgnoreFiltersAsync(Guid roleId, CancellationToken ct = default);
    Task<Role?> GetByNameAsync(string name, Guid tenantId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid roleId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Role role, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
