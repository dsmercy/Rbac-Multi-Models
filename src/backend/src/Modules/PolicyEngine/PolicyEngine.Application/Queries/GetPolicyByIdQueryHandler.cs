using BuildingBlocks.Application;
using PolicyEngine.Application.Common;
using PolicyEngine.Domain.Interfaces;

namespace PolicyEngine.Application.Queries;

public sealed class GetPolicyByIdQueryHandler
    : IQueryHandler<GetPolicyByIdQuery, PolicyDto?>
{
    private readonly IPolicyRepository _policyRepository;

    public GetPolicyByIdQueryHandler(IPolicyRepository policyRepository)
        => _policyRepository = policyRepository;

    public async Task<PolicyDto?> Handle(
        GetPolicyByIdQuery query,
        CancellationToken cancellationToken)
    {
        var policy = await _policyRepository.GetByIdAsync(query.PolicyId, cancellationToken);

        if (policy is null || policy.TenantId != query.TenantId)
            return null;

        return new PolicyDto(
            policy.Id, policy.TenantId, policy.Name, policy.Description,
            policy.Effect, policy.ConditionTreeJson, policy.ResourceId,
            policy.Action, policy.IsActive, policy.CreatedAt, policy.UpdatedAt);
    }
}
