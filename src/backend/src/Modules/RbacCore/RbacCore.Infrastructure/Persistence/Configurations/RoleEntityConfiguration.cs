using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RbacCore.Domain.Entities;

namespace RbacCore.Infrastructure.Persistence.Configurations;

public sealed class RoleEntityConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "rbac");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(500).IsRequired(false);
        builder.Property(r => r.IsSystemRole).HasDefaultValue(false).IsRequired();
        builder.Property(r => r.IsDeleted).HasDefaultValue(false).IsRequired();
        builder.Property(r => r.DeletedAt).IsRequired(false);
        builder.Property(r => r.DeletedBy).IsRequired(false);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired(false);
        builder.Property(r => r.UpdatedBy).IsRequired(false);

        builder.HasMany(r => r.Permissions)
            .WithOne()
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hot-path indexes
        builder.HasIndex(r => new { r.TenantId, r.IsDeleted })
            .HasDatabaseName("IX_Roles_TenantId_IsDeleted");

        builder.HasIndex(r => new { r.TenantId, r.Name, r.IsDeleted })
            .IsUnique()
            .HasDatabaseName("UQ_Roles_TenantId_Name")
            .HasFilter("\"IsDeleted\" = false");
    }
}
