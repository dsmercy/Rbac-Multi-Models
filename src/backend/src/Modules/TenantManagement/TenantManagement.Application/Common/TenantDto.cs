namespace TenantManagement.Application.Common;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    bool IsBootstrapped,
    DateTimeOffset CreatedAt,
    TenantConfigDto Configuration);

public sealed record TenantConfigDto(
    int MaxDelegationChainDepth,
    int PermissionCacheTtlSeconds,
    int TokenVersionCacheTtlSeconds,
    int MaxUsersAllowed,
    int MaxRolesAllowed);
