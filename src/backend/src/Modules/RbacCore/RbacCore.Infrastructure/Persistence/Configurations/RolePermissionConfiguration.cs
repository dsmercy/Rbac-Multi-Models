using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RbacCore.Domain.Entities;

namespace RbacCore.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions", "rbac");
        builder.HasKey(rp => rp.Id);
        // Application generates the PK — EF must not omit it from INSERT
        builder.Property(rp => rp.Id).ValueGeneratedNever();
        builder.Property(rp => rp.RoleId).IsRequired();
        builder.Property(rp => rp.PermissionId).IsRequired();
        builder.Property(rp => rp.TenantId).IsRequired();
        builder.Property(rp => rp.GrantedByUserId).IsRequired();
        builder.Property(rp => rp.GrantedAt).IsRequired();
    }
}
