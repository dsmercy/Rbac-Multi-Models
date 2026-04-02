using BuildingBlocks.Application;
using PolicyEngine.Application.Common;
using PolicyEngine.Domain.Interfaces;

namespace PolicyEngine.Application.Queries;

public sealed class ListPoliciesQueryHandler
    : IQueryHandler<ListPoliciesQuery, IReadOnlyList<PolicyDto>>
{
    private readonly IPolicyRepository _policyRepository;

    public ListPoliciesQueryHandler(IPolicyRepository policyRepository)
        => _policyRepository = policyRepository;

    public async Task<IReadOnlyList<PolicyDto>> Handle(
        ListPoliciesQuery query,
        CancellationToken cancellationToken)
    {
        var policies = await _policyRepository.GetAllByTenantAsync(query.TenantId, cancellationToken);

        return policies
            .Select(p => new PolicyDto(
                p.Id, p.TenantId, p.Name, p.Description,
                p.Effect, p.ConditionTreeJson, p.ResourceId,
                p.Action, p.IsActive, p.CreatedAt, p.UpdatedAt))
            .ToList()
            .AsReadOnly();
    }
}
