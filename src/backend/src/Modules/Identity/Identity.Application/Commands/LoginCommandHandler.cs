using BuildingBlocks.Application;
using Identity.Application.Services;
using Identity.Domain.Interfaces;
using MediatR;

namespace Identity.Application.Commands;

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, TokenPair>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserCredentialRepository _credentialRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IPublisher _publisher;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IUserCredentialRepository credentialRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IPublisher publisher)
    {
        _userRepository = userRepository;
        _credentialRepository = credentialRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _publisher = publisher;
    }

    public async Task<TokenPair> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(
            command.Email, command.TenantId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("User account is inactive.");

        var credential = await _credentialRepository.GetByUserIdAsync(
            user.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (credential.IsLockedOut())
            throw new UnauthorizedAccessException(
                "Account is temporarily locked due to too many failed attempts.");

        if (!_passwordHasher.Verify(command.Password, credential.PasswordHash, credential.PasswordSalt))
        {
            credential.RecordFailedAttempt();
            await _credentialRepository.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        credential.RecordSuccessfulLogin();
        user.RecordLogin();

        var rawRefreshToken = _tokenService.GenerateRawRefreshToken();
        var tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);

        var refreshToken = Domain.Entities.RefreshToken.Create(
            user.Id,
            command.TenantId,
            tokenHash,
            DateTimeOffset.UtcNow.AddDays(30),
            command.IpAddress);

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        // Return raw token in the pair — client stores this, we only store the hash
        var tokenPair = _tokenService.GenerateTokenPair(
            Common.UserMapper.ToDto(user),
            roles: []);

        return tokenPair with { RefreshToken = rawRefreshToken };
    }
}
