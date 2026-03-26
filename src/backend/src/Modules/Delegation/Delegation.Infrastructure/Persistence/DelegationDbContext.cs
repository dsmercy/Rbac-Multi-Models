using BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Delegation.Infrastructure.Persistence;

public sealed class DelegationDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public DelegationDbContext(
        DbContextOptions<DelegationDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Domain.Entities.DelegationGrant> Delegations => Set<Domain.Entities.DelegationGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("delegation");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DelegationDbContext).Assembly);

        if (!_tenantContext.IsSuperAdmin)
        {
            modelBuilder.Entity<Domain.Entities.DelegationGrant>()
                .HasQueryFilter(d =>
                    d.TenantId == _tenantContext.TenantId &&
                    !d.IsRevoked &&
                    d.ExpiresAt > DateTimeOffset.UtcNow);
        }

        base.OnModelCreating(modelBuilder);
    }
}
