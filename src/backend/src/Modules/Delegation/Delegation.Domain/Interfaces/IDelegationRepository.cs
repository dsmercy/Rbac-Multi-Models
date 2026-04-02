using Delegation.Domain.Entities;

namespace Delegation.Domain.Interfaces;

public interface IDelegationRepository
{
    Task<DelegationGrant?> GetByIdAsync(Guid delegationId, CancellationToken ct = default);

    Task<DelegationGrant?> GetActiveDelegationAsync(
        Guid delegateeId, string action, Guid scopeId, Guid tenantId,
        CancellationToken ct = default);

    Task<IReadOnlyList<DelegationGrant>> GetActiveByDelegateeAsync(
        Guid delegateeId, Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<DelegationGrant>> GetActiveByDelegatorAsync(
        Guid delegatorId, Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<DelegationGrant>> GetAllByTenantAsync(
        Guid tenantId, CancellationToken ct = default);

    Task AddAsync(DelegationGrant delegation, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
