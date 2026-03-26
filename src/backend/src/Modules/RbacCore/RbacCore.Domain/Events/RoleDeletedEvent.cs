using BuildingBlocks.Domain;

namespace RbacCore.Domain.Events;

public sealed class RoleDeletedEvent : DomainEvent
{
    public Guid RoleId { get; }
    public Guid DeletedByUserId { get; }

    public RoleDeletedEvent(Guid roleId, Guid tenantId, Guid deletedByUserId)
    {
        RoleId = roleId;
        TenantId = tenantId;
        DeletedByUserId = deletedByUserId;
    }
}
