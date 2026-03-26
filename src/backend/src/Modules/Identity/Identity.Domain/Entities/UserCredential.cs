using BuildingBlocks.Domain;

namespace Identity.Domain.Entities;

public sealed class UserCredential : Entity
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public string PasswordSalt { get; private set; } = null!;
    public DateTimeOffset PasswordUpdatedAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;

    // EF Core constructor
    private UserCredential() { }

    public static UserCredential Create(
        Guid userId,
        Guid tenantId,
        string passwordHash,
        string passwordSalt)
    {
        if (userId == Guid.Empty)
            throw new DomainException("INVALID_USER_ID", "UserId cannot be empty.");

        return new UserCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            PasswordUpdatedAt = DateTimeOffset.UtcNow,
            FailedLoginAttempts = 0
        };
    }

    public void UpdatePassword(string newPasswordHash, string newPasswordSalt)
    {
        PasswordHash = newPasswordHash;
        PasswordSalt = newPasswordSalt;
        PasswordUpdatedAt = DateTimeOffset.UtcNow;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    public void RecordFailedAttempt()
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts >= MaxFailedAttempts)
            LockedUntil = DateTimeOffset.UtcNow.AddMinutes(LockoutMinutes);
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    public bool IsLockedOut()
        => LockedUntil.HasValue && LockedUntil.Value > DateTimeOffset.UtcNow;
}
