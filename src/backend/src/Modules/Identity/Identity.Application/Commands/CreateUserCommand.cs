using BuildingBlocks.Application;
using Identity.Application.Common;

namespace Identity.Application.Commands;

public sealed record CreateUserCommand(
    Guid TenantId,
    string Email,
    string DisplayName,
    string Password,
    Guid CreatedByUserId) : ICommand<UserDto>;
