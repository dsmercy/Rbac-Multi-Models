using BuildingBlocks.Application;
using Identity.Application.Common;

namespace Identity.Application.Queries;

public sealed record GetUserByEmailQuery(
    string Email,
    Guid TenantId) : IQuery<UserDto?>;
