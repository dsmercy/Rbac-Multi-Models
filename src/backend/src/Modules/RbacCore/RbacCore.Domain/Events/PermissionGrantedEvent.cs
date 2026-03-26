using BuildingBlocks.Domain;

namespace RbacCore.Domain.Events;

public sealed class PermissionGrantedEvent : DomainEvent
{
    public Guid RoleId { get; }
    public Guid PermissionId { get; }
    public Guid GrantedByUserId { get; }

    public PermissionGrantedEvent(
        Guid roleId,
        Guid tenantId,
        Guid permissionId,
        Guid grantedByUserId)
    {
        RoleId = roleId;
        TenantId = tenantId;
        PermissionId = permissionId;
        GrantedByUserId = grantedByUserId;
    }
}
