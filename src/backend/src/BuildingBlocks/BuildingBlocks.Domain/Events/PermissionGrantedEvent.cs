namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by RbacCore module when a permission is granted to a role.
/// </summary>
public sealed class PermissionGrantedEvent : DomainEvent
{
    public Guid PermissionId    { get; }
    public Guid RoleId          { get; }
    public Guid GrantedByUserId { get; }

    public PermissionGrantedEvent(Guid permissionId, Guid tenantId, Guid roleId, Guid grantedByUserId)
    {
        PermissionId    = permissionId;
        TenantId        = tenantId;
        RoleId          = roleId;
        GrantedByUserId = grantedByUserId;
    }
}
