using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("user_credentials");

        builder.HasKey(uc => uc.Id);

        builder.Property(uc => uc.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(uc => uc.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(uc => uc.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(uc => uc.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(uc => uc.PasswordSalt)
            .HasColumnName("password_salt")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(uc => uc.PasswordUpdatedAt)
            .HasColumnName("password_updated_at")
            .IsRequired();

        builder.Property(uc => uc.FailedLoginAttempts)
            .HasColumnName("failed_login_attempts")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(uc => uc.LockedUntil)
            .HasColumnName("locked_until");

        builder.HasIndex(uc => uc.UserId)
            .HasDatabaseName("ix_user_credentials_user_id")
            .IsUnique();
    }
}
