using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Identity.Application.Services;
using Identity.Domain.Interfaces;
using MediatR;

namespace Identity.Application.Commands;

/// <summary>
/// Handles user login and issues a full Phase 4 JWT.
///
/// Flow:
///   1. Validate user exists and is active.
///   2. Validate credentials; handle lockout.
///   3. Resolve login info: role names, scope IDs, super-admin flag.
///   4. Read current token version from Redis.
///   5. Generate access + refresh token pair with all Phase 4 claims.
///   6. Persist refresh token; record login timestamp.
/// </summary>
public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, TokenPair>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserCredentialRepository _credentialRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IUserRoleProvider _userRoleProvider;
    private readonly ITokenVersionService _tokenVersionService;
    private readonly IPublisher _publisher;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IUserCredentialRepository credentialRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IUserRoleProvider userRoleProvider,
        ITokenVersionService tokenVersionService,
        IPublisher publisher)
    {
        _userRepository = userRepository;
        _credentialRepository = credentialRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _userRoleProvider = userRoleProvider;
        _tokenVersionService = tokenVersionService;
        _publisher = publisher;
    }

    public async Task<TokenPair> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        // ── 1. User lookup ────────────────────────────────────────────────────
        var user = await _userRepository.GetByEmailAsync(
            command.Email, command.TenantId, cancellationToken)
            ?? throw new InvalidCredentialsException("Invalid credentials.");

        if (!user.IsActive)
            throw new InvalidCredentialsException("User account is inactive.");

        // ── 2. Credential validation ──────────────────────────────────────────
        var credential = await _credentialRepository.GetByUserIdAsync(
            user.Id, cancellationToken)
            ?? throw new InvalidCredentialsException("Invalid credentials.");

        if (credential.IsLockedOut())
            throw new InvalidCredentialsException(
                "Account is temporarily locked due to too many failed attempts.");

        if (!_passwordHasher.Verify(command.Password, credential.PasswordHash, credential.PasswordSalt))
        {
            credential.RecordFailedAttempt();
            await _credentialRepository.SaveChangesAsync(cancellationToken);
            throw new InvalidCredentialsException("Invalid credentials.");
        }

        credential.RecordSuccessfulLogin();
        user.RecordLogin();

        // ── 3. Resolve login info (roles, scopes, super-admin flag) ───────────
        // Uses a direct Dapper query via IUserRoleProvider to avoid a circular
        // dependency through RbacCore.Application.
        var loginInfo = await _userRoleProvider.GetLoginInfoAsync(
            user.Id, command.TenantId, cancellationToken);

        // ── 4. Read current token version from Redis ──────────────────────────
        // Embedded in the JWT as the "tv" claim.
        // TokenVersionValidationStep compares this against the live Redis value
        // on every permission-engine evaluation to detect stale tokens.
        var tokenVersion = await _tokenVersionService.GetTokenVersionAsync(
            user.Id, cancellationToken);

        // ── 5. Issue refresh token ────────────────────────────────────────────
        var rawRefreshToken = _tokenService.GenerateRawRefreshToken();
        var tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);

        var refreshToken = Domain.Entities.RefreshToken.Create(
            user.Id,
            command.TenantId,
            tokenHash,
            DateTimeOffset.UtcNow.AddDays(7),
            command.IpAddress);

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        // ── 6. Generate full Phase 4 JWT ──────────────────────────────────────
        var tokenPair = _tokenService.GenerateTokenPair(new TokenGenerationParams(
            User: Common.UserMapper.ToDto(user),
            RoleNames: loginInfo.RoleNames,
            ScopeIds: loginInfo.ScopeIds,
            IsSuperAdmin: loginInfo.IsSuperAdmin,
            TokenVersion: tokenVersion));

        // Return the raw refresh token (the hash is stored in DB, never the raw value)
        return tokenPair with { RefreshToken = rawRefreshToken };
    }
}
