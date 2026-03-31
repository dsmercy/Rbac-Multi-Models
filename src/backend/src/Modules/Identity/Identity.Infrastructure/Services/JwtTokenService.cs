using Identity.Application.Common;
using Identity.Application.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Infrastructure.Services;

public sealed class JwtSettings
{
    public string SigningKey { get; init; } = null!;
    public string Issuer { get; init; } = null!;
    public string Audience { get; init; } = null!;

    /// <summary>Access token lifetime. Default: 15 minutes per spec.</summary>
    public int AccessTokenExpiryMinutes { get; init; } = 15;
}

/// <summary>
/// Generates JWTs with the full Phase 4 claims schema.
///
/// JWT payload spec:
/// {
///   "sub":           "uuid",               // user ID
///   "tid":           "uuid",               // tenant ID
///   "email":         "string",
///   "roles":         ["name1","name2"],    // embedded role names
///   "scp":           ["scope:{uuid}"],     // embedded scope IDs
///   "del":           "uuid | null",        // delegator ID (if acting on behalf)
///   "del_chain":     ["uuid1","uuid2"],    // ordered delegation chain
///   "is_super_admin": false,
///   "tv":            0,                   // token version (Redis: token-version:{uid})
///   "jti":           "uuid",
///   "iat":           epoch,
///   "exp":           epoch
/// }
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
        => _settings = settings.Value;

    /// <inheritdoc />
    public TokenPair GenerateTokenPair(TokenGenerationParams parameters)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes);

        var claims = BuildClaims(parameters);

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.SigningKey));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256));

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var rawRefresh = GenerateRawRefreshToken();

        return new TokenPair(accessToken, rawRefresh, expiresAt, parameters.TokenVersion);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static IEnumerable<Claim> BuildClaims(TokenGenerationParams p)
    {
        var user = p.User;
        var claims = new List<Claim>
        {
            // Standard OIDC / RFC 7519
            new(JwtRegisteredClaimNames.Sub,  user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),

            // Tenant isolation — validated by TenantValidationMiddleware
            new("tid",           user.TenantId.ToString()),
            new("display_name",  user.DisplayName),

            // Token version — validated by TokenVersionValidationStep (step 0)
            // Incremented on: UserRoleAssigned, UserRoleRevoked,
            //                 DelegationCreated, DelegationRevoked
            new("tv", p.TokenVersion.ToString()),

            // Super-admin bypass flag
            new("is_super_admin", p.IsSuperAdmin.ToString().ToLowerInvariant()),
        };

        // Role names — embedded for performance (avoids DB round-trip per request)
        foreach (var role in p.RoleNames)
            claims.Add(new Claim("roles", role));

        // Scope IDs where user has assignments — format: "scope:{uuid}"
        foreach (var scopeId in p.ScopeIds)
            claims.Add(new Claim("scp", $"scope:{scopeId}"));

        // Delegation chain — present only when token is issued for a delegatee
        if (p.DelegatorId.HasValue)
            claims.Add(new Claim("del", p.DelegatorId.Value.ToString()));

        if (p.DelegationChain is { Count: > 0 })
            foreach (var chainId in p.DelegationChain)
                claims.Add(new Claim("del_chain", chainId.ToString()));

        return claims;
    }

    /// <inheritdoc />
    public string GenerateRawRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    /// <inheritdoc />
    public string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }
}
