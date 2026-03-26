using BuildingBlocks.Application;
using Identity.Application.Services;

namespace Identity.Application.Commands;

public sealed record LoginCommand(
    Guid TenantId,
    string Email,
    string Password,
    string? IpAddress) : ICommand<TokenPair>;
