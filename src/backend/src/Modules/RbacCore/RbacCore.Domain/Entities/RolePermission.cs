using BuildingBlocks.Domain;

namespace RbacCore.Domain.Entities;

public sealed class RolePermission : Entity
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }

    // EF Core constructor
    private RolePermission() { }

    internal static RolePermission Create(
        Guid roleId,
        Guid tenantId,
        Guid permissionId,
        Guid grantedByUserId)
    {
        return new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = roleId,
            PermissionId = permissionId,
            TenantId = tenantId,
            GrantedByUserId = grantedByUserId,
            GrantedAt = DateTimeOffset.UtcNow
        };
    }
}
