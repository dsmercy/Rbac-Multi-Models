using BuildingBlocks.Application;
using RbacCore.Application.Common;

namespace RbacCore.Application.Queries;

public sealed record ListScopesQuery(Guid TenantId) : IQuery<IReadOnlyList<ScopeDto>>;
