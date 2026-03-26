using Identity.Domain.Entities;
using Identity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

public sealed class UserCredentialRepository : IUserCredentialRepository
{
    private readonly IdentityDbContext _context;

    public UserCredentialRepository(IdentityDbContext context)
        => _context = context;

    public async Task<UserCredential?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.UserCredentials
            .FirstOrDefaultAsync(uc => uc.UserId == userId, ct);

    public async Task AddAsync(UserCredential credential, CancellationToken ct = default)
        => await _context.UserCredentials.AddAsync(credential, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
