using BuildingBlocks.Application;
using Identity.Application.Common;

namespace Identity.Application.Queries;

public sealed record GetUsersByIdsQuery(
    IEnumerable<Guid> UserIds,
    Guid TenantId) : IQuery<IReadOnlyList<UserDto>>;
