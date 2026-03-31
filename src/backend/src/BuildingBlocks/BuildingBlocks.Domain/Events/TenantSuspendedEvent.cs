namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by TenantManagement module when a tenant is suspended.
/// Consumed cross-module by: PermissionEngine (flush all tenant cache), AuditLogging.
/// </summary>
public sealed class TenantSuspendedEvent : DomainEvent
{
    public string Reason           { get; }
    public Guid   SuspendedByUserId { get; }

    public TenantSuspendedEvent(Guid tenantId, string reason, Guid suspendedByUserId)
    {
        TenantId          = tenantId;
        Reason            = reason;
        SuspendedByUserId = suspendedByUserId;
    }
}
