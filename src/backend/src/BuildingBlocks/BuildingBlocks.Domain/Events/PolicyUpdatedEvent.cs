namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by PolicyEngine module when a policy is updated.
/// Consumed cross-module by: PermissionEngine (cache eviction), AuditLogging.
/// </summary>
public sealed class PolicyUpdatedEvent : DomainEvent
{
    public Guid PolicyId        { get; }
    public Guid UpdatedByUserId { get; }

    public PolicyUpdatedEvent(Guid policyId, Guid tenantId, Guid updatedByUserId)
    {
        PolicyId        = policyId;
        TenantId        = tenantId;
        UpdatedByUserId = updatedByUserId;
    }
}
