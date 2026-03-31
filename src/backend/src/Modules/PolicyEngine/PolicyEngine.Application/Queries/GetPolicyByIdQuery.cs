using BuildingBlocks.Application;
using PolicyEngine.Application.Common;

namespace PolicyEngine.Application.Queries;

public sealed record GetPolicyByIdQuery(Guid TenantId, Guid PolicyId) : IQuery<PolicyDto?>;
