using BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;
using PolicyEngine.Domain.Entities;

namespace PolicyEngine.Infrastructure.Persistence;

public sealed class PolicyDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public PolicyDbContext(
        DbContextOptions<PolicyDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Policy> Policies => Set<Policy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("policy");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PolicyDbContext).Assembly);

        if (!_tenantContext.IsSuperAdmin)
        {
            modelBuilder.Entity<Policy>()
                .HasQueryFilter(p =>
                    p.TenantId == _tenantContext.TenantId && !p.IsDeleted);
        }

        base.OnModelCreating(modelBuilder);
    }
}
