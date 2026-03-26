using Identity.Application.Common;

namespace Identity.Application.Services;

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);

public interface ITokenService
{
    TokenPair GenerateTokenPair(UserDto user, IEnumerable<string> roles);
    string HashRefreshToken(string rawToken);
    string GenerateRawRefreshToken();
}
