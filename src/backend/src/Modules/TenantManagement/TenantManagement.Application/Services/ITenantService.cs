using TenantManagement.Application.Common;

namespace TenantManagement.Application.Services;

/// <summary>
/// Public anti-corruption interface exposed to other modules.
/// Other modules must only depend on this interface.
/// </summary>
public interface ITenantService
{
    Task<TenantDto?> GetTenantByIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> TenantIsActiveAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantConfigDto> GetConfigAsync(Guid tenantId, CancellationToken ct = default);
}
