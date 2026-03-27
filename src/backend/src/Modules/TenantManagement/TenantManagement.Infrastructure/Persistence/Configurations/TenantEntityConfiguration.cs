using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantManagement.Domain.Entities;

namespace TenantManagement.Infrastructure.Persistence.Configurations;

public sealed class TenantEntityConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants", "tenant");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.OwnsOne(t => t.Slug, slug =>
        {
            slug.Property(s => s.Value)
                .HasColumnName("Slug")
                .HasMaxLength(63)
                .IsRequired();

            // Unique slug defined inside OwnsOne so EF resolves the type at design time
            slug.HasIndex(s => s.Value)
                .HasDatabaseName("UQ_Tenants_Slug")
                .IsUnique();
        });

        builder.OwnsOne(t => t.Configuration, cfg =>
        {
            cfg.Property(c => c.MaxDelegationChainDepth)
                .HasColumnName("MaxDelegationChainDepth")
                .HasDefaultValue(1)
                .IsRequired();

            cfg.Property(c => c.PermissionCacheTtlSeconds)
                .HasColumnName("PermissionCacheTtlSeconds")
                .HasDefaultValue(300)
                .IsRequired();

            cfg.Property(c => c.TokenVersionCacheTtlSeconds)
                .HasColumnName("TokenVersionCacheTtlSeconds")
                .HasDefaultValue(3600)
                .IsRequired();

            cfg.Property(c => c.MaxUsersAllowed)
                .HasColumnName("MaxUsersAllowed")
                .HasDefaultValue(500)
                .IsRequired();

            cfg.Property(c => c.MaxRolesAllowed)
                .HasColumnName("MaxRolesAllowed")
                .HasDefaultValue(100)
                .IsRequired();
        });

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(t => t.IsBootstrapped)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.SuspendedAt).IsRequired(false);
        builder.Property(t => t.SuspensionReason).HasMaxLength(500).IsRequired(false);
        builder.Property(t => t.DeletedAt).IsRequired(false);
        builder.Property(t => t.DeletedBy).IsRequired(false);
        builder.Property(t => t.UpdatedAt).IsRequired(false);
        builder.Property(t => t.UpdatedBy).IsRequired(false);

        builder.HasIndex(t => t.IsDeleted)
            .HasDatabaseName("IX_Tenants_IsDeleted");
    }
}
