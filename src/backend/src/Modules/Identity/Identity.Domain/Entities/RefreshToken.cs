using BuildingBlocks.Domain;

namespace Identity.Domain.Entities;

public sealed class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedByIp { get; private set; }

    // EF Core constructor
    private RefreshToken() { }

    public static RefreshToken Create(
        Guid userId,
        Guid tenantId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? createdByIp = null)
    {
        if (expiresAt <= DateTimeOffset.UtcNow)
            throw new DomainException("INVALID_TOKEN_EXPIRY", "Token expiry must be in the future.");

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByIp = createdByIp
        };
    }

    public void Revoke(string reason)
    {
        if (IsRevoked)
            throw new DomainException("TOKEN_ALREADY_REVOKED", "Refresh token is already revoked.");

        IsRevoked = true;
        RevokedAt = DateTimeOffset.UtcNow;
        RevokedReason = reason;
    }

    public bool IsExpired() => DateTimeOffset.UtcNow >= ExpiresAt;

    public bool IsActive() => !IsRevoked && !IsExpired();
}
