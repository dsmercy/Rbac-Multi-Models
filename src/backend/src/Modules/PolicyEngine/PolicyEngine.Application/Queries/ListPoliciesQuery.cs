using BuildingBlocks.Application;
using PolicyEngine.Application.Common;

namespace PolicyEngine.Application.Queries;

public sealed record ListPoliciesQuery(Guid TenantId) : IQuery<IReadOnlyList<PolicyDto>>;
