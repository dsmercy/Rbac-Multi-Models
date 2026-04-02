using Identity.Domain.Entities;
using Identity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _context;

    public UserRepository(IdentityDbContext context)
        => _context = context;

    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => !u.IsDeleted && u.Id == userId, ct);

    public async Task<User?> GetByEmailAsync(
    string email, Guid tenantId, CancellationToken ct = default)
    => await _context.Users
        .IgnoreQueryFilters()                          // bypass filter � no JWT yet
        .FirstOrDefaultAsync(u =>
            !u.IsDeleted &&
            u.TenantId == tenantId &&
            u.Email.Value == email.ToLowerInvariant(), ct);

    public async Task<bool> ExistsAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        => await _context.Users
            .AnyAsync(u => u.Id == userId && u.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        Guid tenantId,
        CancellationToken ct = default)
        => await _context.Users
            .Where(u => ids.Contains(u.Id) && u.TenantId == tenantId)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetByTenantAsync(
        Guid tenantId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Users
            .IgnoreQueryFilters()
            .Where(u => !u.IsDeleted && u.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLowerInvariant();
            query = query.Where(u =>
                u.Email.Value.Contains(lower) ||
                u.DisplayName.Value.ToLower().Contains(lower));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.Email.Value)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _context.Users.AddAsync(user, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
