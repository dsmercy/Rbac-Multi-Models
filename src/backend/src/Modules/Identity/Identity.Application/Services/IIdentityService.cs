using Identity.Application.Common;

namespace Identity.Application.Services;

/// <summary>
/// Public anti-corruption interface exposed to other modules.
/// Other modules must only depend on this interface, never on
/// Identity.Infrastructure or Identity.Domain directly.
/// </summary>
public interface IIdentityService
{
    Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserDto?> GetUserByEmailAsync(string email, Guid tenantId, CancellationToken ct = default);
    Task<bool> UserExistsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetUsersByIdsAsync(IEnumerable<Guid> ids, Guid tenantId, CancellationToken ct = default);
}
