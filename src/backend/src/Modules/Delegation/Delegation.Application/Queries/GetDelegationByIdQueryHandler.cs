using BuildingBlocks.Application;
using Delegation.Application.Common;
using Delegation.Domain.Interfaces;

namespace Delegation.Application.Queries;

public sealed class GetDelegationByIdQueryHandler
    : IQueryHandler<GetDelegationByIdQuery, ActiveDelegationDto?>
{
    private readonly IDelegationRepository _delegationRepository;

    public GetDelegationByIdQueryHandler(IDelegationRepository delegationRepository)
        => _delegationRepository = delegationRepository;

    public async Task<ActiveDelegationDto?> Handle(
        GetDelegationByIdQuery query,
        CancellationToken cancellationToken)
    {
        var delegation = await _delegationRepository.GetByIdAsync(query.DelegationId, cancellationToken);

        if (delegation is null || delegation.TenantId != query.TenantId)
            return null;

        return new ActiveDelegationDto(
            delegation.Id,
            delegation.TenantId,
            delegation.DelegatorId,
            delegation.DelegateeId,
            delegation.PermissionCodes,
            delegation.ScopeId,
            delegation.ExpiresAt,
            delegation.ChainDepth,
            delegation.CreatedAt);
    }
}
