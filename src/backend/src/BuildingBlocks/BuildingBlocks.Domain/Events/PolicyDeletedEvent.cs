namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by PolicyEngine module when a policy is deleted.
/// Consumed cross-module by: PermissionEngine (cache eviction), AuditLogging.
/// </summary>
public sealed class PolicyDeletedEvent : DomainEvent
{
    public Guid PolicyId        { get; }
    public Guid DeletedByUserId { get; }

    public PolicyDeletedEvent(Guid policyId, Guid tenantId, Guid deletedByUserId)
    {
        PolicyId        = policyId;
        TenantId        = tenantId;
        DeletedByUserId = deletedByUserId;
    }
}
