using BuildingBlocks.Application;
using Identity.Application.Common;

namespace Identity.Application.Queries;

public sealed record GetUserByIdQuery(
    Guid UserId,
    Guid TenantId) : IQuery<UserDto?>;
