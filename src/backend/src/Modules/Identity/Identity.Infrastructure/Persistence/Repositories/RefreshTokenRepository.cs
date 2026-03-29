using Identity.Domain.Entities;
using Identity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _context;

    public RefreshTokenRepository(IdentityDbContext context)
        => _context = context;

    public async Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash, CancellationToken ct = default)
        => await _context.RefreshTokens
            .IgnoreQueryFilters()                    // no JWT during token refresh
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
        => await _context.RefreshTokens
            .Where(rt =>
                rt.UserId == userId &&
                rt.TenantId == tenantId &&
                !rt.IsRevoked &&
                rt.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        => await _context.RefreshTokens.AddAsync(token, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
