using BuildingBlocks.Application;
using Delegation.Application.Common;

namespace Delegation.Application.Queries;

public sealed record GetDelegationByIdQuery(Guid TenantId, Guid DelegationId) : IQuery<ActiveDelegationDto?>;
