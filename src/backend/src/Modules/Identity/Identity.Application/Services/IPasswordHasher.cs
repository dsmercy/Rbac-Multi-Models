namespace Identity.Application.Services;

public interface IPasswordHasher
{
    (string Hash, string Salt) HashPassword(string plainText);
    bool Verify(string plainText, string hash, string salt);
}
