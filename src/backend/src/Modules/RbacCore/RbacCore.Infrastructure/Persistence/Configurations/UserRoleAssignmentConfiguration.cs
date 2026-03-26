using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RbacCore.Domain.Entities;

namespace RbacCore.Infrastructure.Persistence.Configurations;

public sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("UserRoleAssignments", "rbac");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.RoleId).IsRequired();
        builder.Property(a => a.ScopeId).IsRequired(false);
        builder.Property(a => a.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(a => a.ExpiresAt).IsRequired(false);
        builder.Property(a => a.DeactivatedReason).HasMaxLength(200).IsRequired(false);
        builder.Property(a => a.DeactivatedAt).IsRequired(false);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.CreatedBy).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired(false);
        builder.Property(a => a.UpdatedBy).IsRequired(false);

        // Hot-path: looking up active assignments for a user in a tenant+scope
        builder.HasIndex(a => new { a.TenantId, a.UserId, a.IsActive })
            .HasDatabaseName("IX_UserRoleAssignments_TenantId_UserId_IsActive");

        // Hot-path: cascade deactivation on role delete
        builder.HasIndex(a => new { a.TenantId, a.RoleId, a.IsActive })
            .HasDatabaseName("IX_UserRoleAssignments_TenantId_RoleId_IsActive");

        builder.HasIndex(a => new { a.TenantId, a.UserId, a.ScopeId, a.IsActive })
            .HasDatabaseName("IX_UserRoleAssignments_TenantId_UserId_ScopeId_IsActive");
    }
}
