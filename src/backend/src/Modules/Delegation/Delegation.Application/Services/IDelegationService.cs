using Delegation.Application.Common;

namespace Delegation.Application.Services;

/// <summary>
/// Public anti-corruption interface consumed by the PermissionEngine pipeline.
/// </summary>
public interface IDelegationService
{
    Task<ActiveDelegationDto?> GetActiveDelegationAsync(
        Guid delegateeId, string action, Guid scopeId, Guid tenantId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ActiveDelegationDto>> GetDelegationsForUserAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default);
}
