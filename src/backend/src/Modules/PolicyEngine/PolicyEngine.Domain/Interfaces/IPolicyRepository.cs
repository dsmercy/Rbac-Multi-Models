using PolicyEngine.Domain.Entities;

namespace PolicyEngine.Domain.Interfaces;

public interface IPolicyRepository
{
    Task<Policy?> GetByIdAsync(Guid policyId, CancellationToken ct = default);
    Task<IReadOnlyList<Policy>> GetActivePoliciesAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Policy>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Policy>> GetByResourceAsync(Guid resourceId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Policy>> GetGlobalPoliciesAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Policy policy, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
