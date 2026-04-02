using BuildingBlocks.Application;
using Delegation.Application.Common;

namespace Delegation.Application.Queries;

public sealed record ListDelegationsQuery(Guid TenantId) : IQuery<IReadOnlyList<ActiveDelegationDto>>;
