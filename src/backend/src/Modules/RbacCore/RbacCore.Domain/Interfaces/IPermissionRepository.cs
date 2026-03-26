using RbacCore.Domain.Entities;

namespace RbacCore.Domain.Interfaces;

public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(Guid permissionId, CancellationToken ct = default);
    Task<Permission?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetByCodesAsync(IEnumerable<string> codes, Guid tenantId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid permissionId, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Permission permission, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
