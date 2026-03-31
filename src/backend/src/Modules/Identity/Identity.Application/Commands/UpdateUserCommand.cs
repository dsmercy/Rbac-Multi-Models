using BuildingBlocks.Application;
using Identity.Application.Common;

namespace Identity.Application.Commands;

public sealed record UpdateUserCommand(
    Guid TenantId,
    Guid UserId,
    string DisplayName,
    Guid UpdatedByUserId) : ICommand<UserDto>;
