namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by TenantManagement module when a tenant is created.
/// Consumed cross-module by: AuditLogging.
/// </summary>
public sealed class TenantCreatedEvent : DomainEvent
{
    public string Name            { get; }
    public string Slug            { get; }
    public Guid   CreatedByUserId { get; }

    public TenantCreatedEvent(Guid tenantId, string name, string slug, Guid createdByUserId)
    {
        TenantId        = tenantId;
        Name            = name;
        Slug            = slug;
        CreatedByUserId = createdByUserId;
    }
}
