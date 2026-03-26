using Identity.Application.Services;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Infrastructure.Services;

/// <summary>
/// PBKDF2-SHA512 password hasher. Production deployments should use Argon2id
/// via the Konscious.Security.Cryptography.Argon2 NuGet package.
/// This implementation uses PBKDF2 as a portable fallback with 310,000 iterations
/// matching OWASP minimum recommendations for PBKDF2-HMAC-SHA512.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 32;
    private const int HashSize = 64;
    private const int Iterations = 310_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public (string Hash, string Salt) HashPassword(string plainText)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plainText),
            saltBytes,
            Iterations,
            Algorithm,
            HashSize);

        return (
            Convert.ToBase64String(hashBytes),
            Convert.ToBase64String(saltBytes)
        );
    }

    public bool Verify(string plainText, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plainText),
            saltBytes,
            Iterations,
            Algorithm,
            HashSize);

        return CryptographicOperations.FixedTimeEquals(
            hashBytes,
            Convert.FromBase64String(hash));
    }
}
