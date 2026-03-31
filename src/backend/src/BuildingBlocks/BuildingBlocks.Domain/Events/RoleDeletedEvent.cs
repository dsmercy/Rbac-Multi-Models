namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by RbacCore module when a role is soft-deleted.
/// Consumed cross-module by: AuditLogging, PermissionEngine (cache eviction).
/// </summary>
public sealed class RoleDeletedEvent : DomainEvent
{
    public Guid RoleId          { get; }
    public Guid DeletedByUserId { get; }

    public RoleDeletedEvent(Guid roleId, Guid tenantId, Guid deletedByUserId)
    {
        RoleId          = roleId;
        TenantId        = tenantId;
        DeletedByUserId = deletedByUserId;
    }
}
