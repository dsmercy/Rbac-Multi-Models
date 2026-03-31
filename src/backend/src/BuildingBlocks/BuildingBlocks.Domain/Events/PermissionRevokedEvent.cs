namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by RbacCore module when a permission is revoked from a role.
/// </summary>
public sealed class PermissionRevokedEvent : DomainEvent
{
    public Guid PermissionId    { get; }
    public Guid RoleId          { get; }
    public Guid RevokedByUserId { get; }

    public PermissionRevokedEvent(Guid permissionId, Guid tenantId, Guid roleId, Guid revokedByUserId)
    {
        PermissionId    = permissionId;
        TenantId        = tenantId;
        RoleId          = roleId;
        RevokedByUserId = revokedByUserId;
    }
}
