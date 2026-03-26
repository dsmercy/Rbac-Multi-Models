using BuildingBlocks.Domain;

namespace TenantManagement.Domain.ValueObjects;

public sealed class TenantConfiguration : ValueObject
{
    public int MaxDelegationChainDepth { get; }
    public int PermissionCacheTtlSeconds { get; }
    public int TokenVersionCacheTtlSeconds { get; }
    public int MaxUsersAllowed { get; }
    public int MaxRolesAllowed { get; }

    private TenantConfiguration(
        int maxDelegationChainDepth,
        int permissionCacheTtlSeconds,
        int tokenVersionCacheTtlSeconds,
        int maxUsersAllowed,
        int maxRolesAllowed)
    {
        MaxDelegationChainDepth = maxDelegationChainDepth;
        PermissionCacheTtlSeconds = permissionCacheTtlSeconds;
        TokenVersionCacheTtlSeconds = tokenVersionCacheTtlSeconds;
        MaxUsersAllowed = maxUsersAllowed;
        MaxRolesAllowed = maxRolesAllowed;
    }

    public static TenantConfiguration CreateDefault() => new(
        maxDelegationChainDepth: 1,
        permissionCacheTtlSeconds: 300,
        tokenVersionCacheTtlSeconds: 3600,
        maxUsersAllowed: 500,
        maxRolesAllowed: 100);

    public static TenantConfiguration Create(
        int maxDelegationChainDepth,
        int permissionCacheTtlSeconds,
        int tokenVersionCacheTtlSeconds,
        int maxUsersAllowed,
        int maxRolesAllowed)
    {
        if (maxDelegationChainDepth < 1 || maxDelegationChainDepth > 3)
            throw new DomainException("INVALID_CONFIG", "MaxDelegationChainDepth must be between 1 and 3 (platform hard limit).");

        if (permissionCacheTtlSeconds < 30 || permissionCacheTtlSeconds > 3600)
            throw new DomainException("INVALID_CONFIG", "PermissionCacheTtlSeconds must be between 30 and 3600.");

        return new TenantConfiguration(
            maxDelegationChainDepth,
            permissionCacheTtlSeconds,
            tokenVersionCacheTtlSeconds,
            maxUsersAllowed,
            maxRolesAllowed);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MaxDelegationChainDepth;
        yield return PermissionCacheTtlSeconds;
        yield return TokenVersionCacheTtlSeconds;
        yield return MaxUsersAllowed;
        yield return MaxRolesAllowed;
    }
}
