using BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;
using RbacCore.Domain.Entities;

namespace RbacCore.Infrastructure.Persistence;

public sealed class RbacDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public RbacDbContext(
        DbContextOptions<RbacDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<Scope> Scopes => Set<Scope>();
    public DbSet<ScopeHierarchy> ScopeHierarchies => Set<ScopeHierarchy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("rbac");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RbacDbContext).Assembly);

        if (!_tenantContext.IsSuperAdmin)
        {
            modelBuilder.Entity<Role>()
                .HasQueryFilter(r => r.TenantId == _tenantContext.TenantId && !r.IsDeleted);

            modelBuilder.Entity<Permission>()
                .HasQueryFilter(p => p.TenantId == _tenantContext.TenantId && !p.IsDeleted);

            modelBuilder.Entity<RolePermission>()
                .HasQueryFilter(rp => rp.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<UserRoleAssignment>()
                .HasQueryFilter(a =>
                    a.TenantId == _tenantContext.TenantId &&
                    a.IsActive &&
                    (a.ExpiresAt == null || a.ExpiresAt > DateTimeOffset.UtcNow));

            modelBuilder.Entity<Scope>()
                .HasQueryFilter(s => s.TenantId == _tenantContext.TenantId && !s.IsDeleted);

            modelBuilder.Entity<ScopeHierarchy>()
                .HasQueryFilter(sh => sh.TenantId == _tenantContext.TenantId);
        }

        base.OnModelCreating(modelBuilder);
    }
}
