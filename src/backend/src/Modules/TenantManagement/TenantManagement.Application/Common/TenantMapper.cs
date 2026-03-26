using TenantManagement.Application.Common;
using TenantManagement.Domain.Entities;

namespace TenantManagement.Application.Common;

public static class TenantMapper
{
    public static TenantDto ToDto(Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.Slug.Value,
        tenant.IsActive,
        tenant.IsBootstrapped,
        tenant.CreatedAt,
        new TenantConfigDto(
            tenant.Configuration.MaxDelegationChainDepth,
            tenant.Configuration.PermissionCacheTtlSeconds,
            tenant.Configuration.TokenVersionCacheTtlSeconds,
            tenant.Configuration.MaxUsersAllowed,
            tenant.Configuration.MaxRolesAllowed));
}
