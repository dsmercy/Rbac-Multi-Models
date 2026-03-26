using BuildingBlocks.Infrastructure;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());

        // Global query filters — automatically applied to every query
        modelBuilder.Entity<User>()
            .HasQueryFilter(u =>
                !u.IsDeleted &&
                (_tenantContext.IsSuperAdmin || u.TenantId == _tenantContext.TenantId));

        modelBuilder.Entity<UserCredential>()
            .HasQueryFilter(uc =>
                _tenantContext.IsSuperAdmin || uc.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<RefreshToken>()
            .HasQueryFilter(rt =>
                _tenantContext.IsSuperAdmin || rt.TenantId == _tenantContext.TenantId);

        base.OnModelCreating(modelBuilder);
    }
}
