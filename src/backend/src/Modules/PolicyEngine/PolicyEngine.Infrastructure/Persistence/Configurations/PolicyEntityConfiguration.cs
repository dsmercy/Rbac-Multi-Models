using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PolicyEngine.Domain.Entities;

namespace PolicyEngine.Infrastructure.Persistence.Configurations;

public sealed class PolicyEntityConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("Policies", "policy");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000).IsRequired(false);
        builder.Property(p => p.Effect).IsRequired();
        builder.Property(p => p.ConditionTreeJson).HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.ResourceId).IsRequired(false);
        builder.Property(p => p.Action).HasMaxLength(100).IsRequired(false);
        builder.Property(p => p.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(p => p.IsDeleted).HasDefaultValue(false).IsRequired();
        builder.Property(p => p.DeletedAt).IsRequired(false);
        builder.Property(p => p.DeletedBy).IsRequired(false);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired(false);
        builder.Property(p => p.UpdatedBy).IsRequired(false);

        builder.HasIndex(p => new { p.TenantId, p.IsActive, p.IsDeleted })
            .HasDatabaseName("IX_Policies_TenantId_IsActive");

        builder.HasIndex(p => new { p.TenantId, p.ResourceId, p.IsActive })
            .HasDatabaseName("IX_Policies_TenantId_ResourceId_IsActive");
    }
}
