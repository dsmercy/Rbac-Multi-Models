using BuildingBlocks.Application;
using Identity.Application.Common;

namespace Identity.Application.Queries;

public sealed record ListUsersQuery(
    Guid TenantId,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<UserDto>>;
