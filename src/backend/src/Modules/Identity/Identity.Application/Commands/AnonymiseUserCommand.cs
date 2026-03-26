using BuildingBlocks.Application;

namespace Identity.Application.Commands;

public sealed record AnonymiseUserCommand(
    Guid UserId,
    Guid TenantId,
    Guid RequestedByUserId) : ICommand;
