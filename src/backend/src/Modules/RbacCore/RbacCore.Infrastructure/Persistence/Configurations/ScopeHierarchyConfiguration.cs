using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RbacCore.Domain.Entities;

namespace RbacCore.Infrastructure.Persistence.Configurations;

public sealed class ScopeHierarchyConfiguration : IEntityTypeConfiguration<ScopeHierarchy>
{
    public void Configure(EntityTypeBuilder<ScopeHierarchy> builder)
    {
        builder.ToTable("ScopeHierarchy", "rbac");
        builder.HasKey(sh => sh.Id);
        builder.Property(sh => sh.Id).ValueGeneratedNever();
        builder.Property(sh => sh.TenantId).IsRequired();
        builder.Property(sh => sh.AncestorId).IsRequired();
        builder.Property(sh => sh.DescendantId).IsRequired();
        builder.Property(sh => sh.Depth).IsRequired();

        // Unique ancestor+descendant pair per tenant (no duplicate closure rows)
        builder.HasIndex(sh => new { sh.TenantId, sh.AncestorId, sh.DescendantId })
            .IsUnique()
            .HasDatabaseName("UQ_ScopeHierarchy_Ancestor_Descendant");

        // Hot-path: find all ancestors of a given scope (upward traversal)
        builder.HasIndex(sh => new { sh.TenantId, sh.DescendantId, sh.Depth })
            .HasDatabaseName("IX_ScopeHierarchy_TenantId_DescendantId");

        // Hot-path: find all descendants of a given scope (downward traversal)
        builder.HasIndex(sh => new { sh.TenantId, sh.AncestorId, sh.Depth })
            .HasDatabaseName("IX_ScopeHierarchy_TenantId_AncestorId");
    }
}
