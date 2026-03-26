using BuildingBlocks.Application;
using Identity.Application.Services;

namespace Identity.Application.Commands;

public sealed record RefreshTokenCommand(
    string RefreshToken,
    Guid TenantId) : ICommand<TokenPair>;
