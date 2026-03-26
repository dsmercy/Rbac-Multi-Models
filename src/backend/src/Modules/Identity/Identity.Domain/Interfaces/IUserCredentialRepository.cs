using Identity.Domain.Entities;

namespace Identity.Domain.Interfaces;

public interface IUserCredentialRepository
{
    Task<UserCredential?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(UserCredential credential, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
