using BuildingBlocks.Application;
using Delegation.Application.Common;
using Delegation.Domain.Interfaces;

namespace Delegation.Application.Queries;

public sealed class ListDelegationsQueryHandler
    : IQueryHandler<ListDelegationsQuery, IReadOnlyList<ActiveDelegationDto>>
{
    private readonly IDelegationRepository _delegationRepository;

    public ListDelegationsQueryHandler(IDelegationRepository delegationRepository)
        => _delegationRepository = delegationRepository;

    public async Task<IReadOnlyList<ActiveDelegationDto>> Handle(
        ListDelegationsQuery query,
        CancellationToken cancellationToken)
    {
        var delegations = await _delegationRepository.GetAllByTenantAsync(query.TenantId, cancellationToken);

        return delegations
            .Select(d => new ActiveDelegationDto(
                d.Id, d.TenantId, d.DelegatorId, d.DelegateeId,
                d.PermissionCodes, d.ScopeId, d.ExpiresAt, d.ChainDepth,
                d.CreatedAt, d.IsRevoked, d.RevokedAt))
            .ToList()
            .AsReadOnly();
    }
}
