namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by RbacCore module when a role is created.
/// </summary>
public sealed class RoleCreatedEvent : DomainEvent
{
    public Guid   RoleId          { get; }
    public string RoleName        { get; }
    public Guid   CreatedByUserId { get; }

    public RoleCreatedEvent(Guid roleId, Guid tenantId, string roleName, Guid createdByUserId)
    {
        RoleId          = roleId;
        TenantId        = tenantId;
        RoleName        = roleName;
        CreatedByUserId = createdByUserId;
    }
}
