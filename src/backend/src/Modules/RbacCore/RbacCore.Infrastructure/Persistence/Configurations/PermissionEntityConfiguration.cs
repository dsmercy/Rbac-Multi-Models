using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RbacCore.Domain.Entities;

namespace RbacCore.Infrastructure.Persistence.Configurations;

public sealed class PermissionEntityConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", "rbac");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.TenantId).IsRequired();

        // Unique code index defined inside OwnsOne so EF can resolve
        // the owned property type at design time.
        builder.OwnsOne(p => p.Code, code =>
        {
            code.Property(c => c.Value)
                .HasColumnName("Code")
                .HasMaxLength(100)
                .IsRequired();
            // Composite unique index (TenantId, Code) is managed by migration
            // 20260403000001_FixPermissionCodeUniqueIndex_TenantScoped — not tracked by EF model
        });

        builder.Property(p => p.ResourceType).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Action).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(500).IsRequired(false);
        builder.Property(p => p.IsDeleted).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.DeletedAt).IsRequired(false);
        builder.Property(p => p.DeletedBy).IsRequired(false);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired(false);
        builder.Property(p => p.UpdatedBy).IsRequired(false);

        builder.HasIndex(p => new { p.TenantId, p.IsDeleted })
            .HasDatabaseName("IX_Permissions_TenantId_IsDeleted");
    }
}
