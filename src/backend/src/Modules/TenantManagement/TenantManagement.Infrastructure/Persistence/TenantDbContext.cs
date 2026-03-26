using BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TenantManagement.Domain.Entities;

namespace TenantManagement.Infrastructure.Persistence;

public sealed class TenantDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public TenantDbContext(
        DbContextOptions<TenantDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tenant");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantDbContext).Assembly);

        // Tenants table is NOT tenant-scoped (it IS the tenant registry).
        // Super-admin and platform ops read directly; no query filter needed here.
        // Soft-delete filter still applies.
        modelBuilder.Entity<Tenant>()
            .HasQueryFilter(t => !t.IsDeleted);

        base.OnModelCreating(modelBuilder);
    }
}
