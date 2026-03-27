using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delegation.Infrastructure.Persistence.Configurations;

public sealed class DelegationEntityConfiguration
    : IEntityTypeConfiguration<Domain.Entities.DelegationGrant>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.DelegationGrant> builder)
    {
        builder.ToTable("Delegations", "delegation");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.DelegatorId).IsRequired();
        builder.Property(d => d.DelegateeId).IsRequired();
        builder.Property(d => d.ScopeId).IsRequired();
        builder.Property(d => d.ExpiresAt).IsRequired();
        builder.Property(d => d.ChainDepth).IsRequired();
        builder.Property(d => d.IsRevoked).HasDefaultValue(false).IsRequired();
        builder.Property(d => d.RevokedAt).IsRequired(false);
        builder.Property(d => d.RevokedByUserId).IsRequired(false);
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.CreatedBy).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired(false);
        builder.Property(d => d.UpdatedBy).IsRequired(false);

        // Map the backing field _permissionCodes directly by field name.
        // PermissionCodes property is [NotMapped] so EF never sees the
        // read-only interface. The field is List<string> — writable by EF.
        builder.PrimitiveCollection("_permissionCodes")
            .HasColumnName("permission_codes")
            .HasColumnType("text[]")
            .IsRequired();

        // Hot-path indexes
        builder.HasIndex(d => new { d.TenantId, d.DelegateeId, d.IsRevoked })
            .HasDatabaseName("IX_Delegations_TenantId_DelegateeId_IsRevoked");

        builder.HasIndex(d => new { d.TenantId, d.DelegatorId, d.IsRevoked })
            .HasDatabaseName("IX_Delegations_TenantId_DelegatorId_IsRevoked");

        builder.HasIndex(d => new { d.TenantId, d.ExpiresAt })
            .HasDatabaseName("IX_Delegations_TenantId_ExpiresAt");
    }
}
