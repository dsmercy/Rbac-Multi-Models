using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(u => u.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        // OwnsOne: Email.Value is a shadow property inside the owned type.
        // The unique-per-tenant index is defined inside this block so EF
        // already knows the property type — no string-based HasIndex needed.
        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(320)
                .IsRequired();

            // Unique email index per tenant — defined inside OwnsOne so EF
            // can resolve the owned property type at design time.
            email.HasIndex(e => e.Value)
                .HasDatabaseName("ix_users_email")
                .IsUnique();
        });

        builder.OwnsOne(u => u.DisplayName, dn =>
        {
            dn.Property(d => d.Value)
                .HasColumnName("display_name")
                .HasMaxLength(150)
                .IsRequired();
        });

        builder.Property(u => u.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(u => u.AnonymisedMarker)
            .HasColumnName("anonymised_marker")
            .HasMaxLength(64);

        builder.Property(u => u.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(u => u.DeletedBy)
            .HasColumnName("deleted_by");

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(u => u.UpdatedBy)
            .HasColumnName("updated_by");

        // Hot-path: tenant filtering
        builder.HasIndex(u => u.TenantId)
            .HasDatabaseName("ix_users_tenant_id");

        // Soft-delete filter support
        builder.HasIndex(u => new { u.TenantId, u.IsDeleted })
            .HasDatabaseName("ix_users_tenant_is_deleted");
    }
}
