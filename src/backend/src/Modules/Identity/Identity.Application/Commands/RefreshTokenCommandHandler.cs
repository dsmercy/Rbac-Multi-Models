using BuildingBlocks.Application;
using Identity.Application.Services;
using Identity.Domain.Interfaces;

namespace Identity.Application.Commands;

public sealed class RefreshTokenCommandHandler
    : ICommandHandler<RefreshTokenCommand, TokenPair>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        ITokenService tokenService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    public async Task<TokenPair> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashRefreshToken(command.RefreshToken);

        var stored = await _refreshTokenRepository.GetByTokenHashAsync(
            tokenHash, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (!stored.IsActive())
            throw new UnauthorizedAccessException("Refresh token is expired or revoked.");

        if (stored.TenantId != command.TenantId)
            throw new UnauthorizedAccessException("Refresh token does not belong to this tenant.");

        var user = await _userRepository.GetByIdAsync(stored.UserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User no longer exists.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("User account is inactive.");

        // Rotate: revoke old, issue new
        stored.Revoke("Rotated");

        var newRawToken = _tokenService.GenerateRawRefreshToken();
        var newHash = _tokenService.HashRefreshToken(newRawToken);

        var newToken = Domain.Entities.RefreshToken.Create(
            user.Id,
            command.TenantId,
            newHash,
            DateTimeOffset.UtcNow.AddDays(30));

        await _refreshTokenRepository.AddAsync(newToken, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        var pair = _tokenService.GenerateTokenPair(
            Common.UserMapper.ToDto(user), roles: []);

        return pair with { RefreshToken = newRawToken };
    }
}
