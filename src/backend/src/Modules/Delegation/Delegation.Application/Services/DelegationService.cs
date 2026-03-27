using Delegation.Application.Common;
using Delegation.Domain.Interfaces;

namespace Delegation.Application.Services;

public sealed class DelegationService : IDelegationService
{
    private readonly IDelegationRepository _repository;

    public DelegationService(IDelegationRepository repository)
        => _repository = repository;

    public async Task<ActiveDelegationDto?> GetActiveDelegationAsync(
        Guid delegateeId, string action, Guid scopeId, Guid tenantId,
        CancellationToken ct = default)
    {
        var delegation = await _repository.GetActiveDelegationAsync(
            delegateeId, action, scopeId, tenantId, ct);

        if (delegation is null || !delegation.IsActive())
            return null;

        return ToDto(delegation);
    }

    public async Task<IReadOnlyList<ActiveDelegationDto>> GetDelegationsForUserAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var delegations = await _repository.GetActiveByDelegateeAsync(userId, tenantId, ct);
        return delegations.Where(d => d.IsActive()).Select(ToDto).ToList().AsReadOnly();
    }

    private static ActiveDelegationDto ToDto(Domain.Entities.DelegationGrant d) => new(
        d.Id, d.TenantId, d.DelegatorId, d.DelegateeId,
        d.PermissionCodes, d.ScopeId, d.ExpiresAt, d.ChainDepth, d.CreatedAt);
}
