using BuildingBlocks.Application;

namespace Identity.Application.Commands;

public sealed record DeactivateUserCommand(
    Guid UserId,
    Guid TenantId,
    string Reason,
    Guid RequestedByUserId) : ICommand;
