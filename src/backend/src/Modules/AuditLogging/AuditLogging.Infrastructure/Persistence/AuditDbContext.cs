using AuditLogging.Domain.Entities;
using BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AuditLogging.Infrastructure.Persistence;

public sealed class AuditDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AuditDbContext(
        DbContextOptions<AuditDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("audit");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);

        // Audit logs are immutable — no update/delete operations on them.
        // Super-admin reads all tenants; normal reads are tenant-scoped.
        if (!_tenantContext.IsSuperAdmin)
        {
            modelBuilder.Entity<AuditLog>()
                .HasQueryFilter(a => a.TenantId == _tenantContext.TenantId);
        }

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        EnforceAppendOnly();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        EnforceAppendOnly();
        return base.SaveChangesAsync(ct);
    }

    private void EnforceAppendOnly()
    {
        var illegal = ChangeTracker.Entries<AuditLog>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (illegal.Any())
            throw new InvalidOperationException(
                "AuditLog entries are immutable. Update and Delete operations are forbidden.");
    }
}
