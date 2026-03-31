using BuildingBlocks.Application;
using Identity.Application.Services;
using Identity.Domain.Interfaces;

namespace Identity.Application.Commands;

/// <summary>
/// Handles refresh-token rotation and issues a new full Phase 4 JWT.
///
/// Rotation policy: old refresh token is revoked, new one is issued.
/// This invalidates any leaked refresh tokens immediately on first use.
///
/// The new access token re-embeds:
///   • Current role names and scope IDs (may have changed since last login)
///   • Current token version from Redis
///   • Super-admin flag
///
/// Note: If the token version in Redis has been incremented since the last
/// login (role/delegation change), the new JWT will carry the updated version
/// and the stale access token (if any) will be rejected on next permission
/// evaluation, completing the revocation loop.
/// </summary>
public sealed class RefreshTokenCommandHandler
    : ICommandHandler<RefreshTokenCommand, TokenPair>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IUserRoleProvider _userRoleProvider;
    private readonly ITokenVersionService _tokenVersionService;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        ITokenService tokenService,
        IUserRoleProvider userRoleProvider,
        ITokenVersionService tokenVersionService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _userRoleProvider = userRoleProvider;
        _tokenVersionService = tokenVersionService;
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
            throw new UnauthorizedAccessException(
                "Refresh token does not belong to this tenant.");

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
            DateTimeOffset.UtcNow.AddDays(7));

        await _refreshTokenRepository.AddAsync(newToken, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        // Re-resolve roles/scopes — may have changed since last login
        var loginInfo = await _userRoleProvider.GetLoginInfoAsync(
            user.Id, command.TenantId, cancellationToken);

        // Always embed the current token version so the new JWT is valid
        var tokenVersion = await _tokenVersionService.GetTokenVersionAsync(
            user.Id, cancellationToken);

        var pair = _tokenService.GenerateTokenPair(new TokenGenerationParams(
            User: Common.UserMapper.ToDto(user),
            RoleNames: loginInfo.RoleNames,
            ScopeIds: loginInfo.ScopeIds,
            IsSuperAdmin: loginInfo.IsSuperAdmin,
            TokenVersion: tokenVersion));

        return pair with { RefreshToken = newRawToken };
    }
}
