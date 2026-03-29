using BuildingBlocks.Infrastructure;

namespace RbacSystem.Api.Seeding;

/// <summary>
/// ITenantContext implementation used exclusively by DataSeeder.
/// Acts as a platform super-admin so every DbContext bypasses all
/// tenant query filters — required because seeding runs outside
/// of an HTTP request scope (no JWT, no HttpContext).
///
/// DO NOT register this in DI. DataSeeder instantiates it directly
/// and passes it to DbContext constructors.
/// </summary>
internal sealed class SeedTenantContext : ITenantContext
{
    /// <summary>
    /// Not meaningful for super-admin; set to Empty so accidental
    /// usage in tenant-scoped code is obvious.
    /// </summary>
    public Guid TenantId => Guid.Empty;

    /// <summary>
    /// True — causes all EF Core global query filters to be bypassed,
    /// allowing the seeder to read and write across all tenants.
    /// </summary>
    public bool IsSuperAdmin => true;

    /// <summary>
    /// System user sentinel. Audit columns that capture CreatedBy
    /// will record Guid.Empty, which is the documented system sentinel.
    /// </summary>
    public Guid UserId => Guid.Empty;
}
