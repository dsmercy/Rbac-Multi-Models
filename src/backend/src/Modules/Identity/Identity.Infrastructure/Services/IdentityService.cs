using Identity.Application.Common;
using Identity.Application.Services;
using Identity.Domain.Interfaces;

namespace Identity.Infrastructure.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly IUserRepository _userRepository;

    public IdentityService(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        return user is null ? null : UserMapper.ToDto(user);
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email, Guid tenantId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, tenantId, ct);
        return user is null ? null : UserMapper.ToDto(user);
    }

    public Task<bool> UserExistsAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        => _userRepository.ExistsAsync(userId, tenantId, ct);

    public async Task<IReadOnlyList<UserDto>> GetUsersByIdsAsync(
        IEnumerable<Guid> ids,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var users = await _userRepository.GetByIdsAsync(ids, tenantId, ct);
        return users.Select(UserMapper.ToDto).ToList();
    }
}
