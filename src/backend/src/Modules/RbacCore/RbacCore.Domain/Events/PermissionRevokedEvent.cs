using BuildingBlocks.Domain;

namespace RbacCore.Domain.Events;

public sealed class PermissionRevokedEvent : DomainEvent
{
    public Guid RoleId { get; }
    public Guid PermissionId { get; }
    public Guid RevokedByUserId { get; }

    public PermissionRevokedEvent(
        Guid roleId,
        Guid tenantId,
        Guid permissionId,
        Guid revokedByUserId)
    {
        RoleId = roleId;
        TenantId = tenantId;
        PermissionId = permissionId;
        RevokedByUserId = revokedByUserId;
    }
}
