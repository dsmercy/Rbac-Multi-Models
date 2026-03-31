using Identity.Application.Common;

namespace Identity.Application.Services;

/// <summary>
/// Full JWT payload contract for Phase 4.
///
/// JWT claims emitted:
///   sub              — user UUID
///   tid              — tenant UUID
///   email            — user email
///   roles            — role names array (embedded for performance)
///   scp              — scope IDs where user has assignments (e.g. "scope:{uuid}")
///   del              — delegator UUID when this token is issued for a delegatee
///   del_chain        — ordered UUID array of the full delegation chain
///   is_super_admin   — bool; bypasses tenant isolation checks when true
///   tv               — token version (int); validated against Redis on every
///                      permission-engine evaluation to detect stale tokens
///   jti              — unique token ID for revocation lookup
///   iat / exp        — standard OIDC timestamps
/// </summary>
public sealed record TokenPair(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    int TokenVersion);

/// <summary>
/// Parameters for full-claims JWT generation (used at login and refresh).
/// </summary>
public sealed record TokenGenerationParams(
    UserDto User,
    IReadOnlyList<string> RoleNames,
    IReadOnlyList<Guid> ScopeIds,
    bool IsSuperAdmin,
    int TokenVersion,
    Guid? DelegatorId = null,
    IReadOnlyList<Guid>? DelegationChain = null);

public interface ITokenService
{
    /// <summary>Generates an access + refresh token pair with full Phase 4 claims.</summary>
    TokenPair GenerateTokenPair(TokenGenerationParams parameters);

    string HashRefreshToken(string rawToken);
    string GenerateRawRefreshToken();
}
