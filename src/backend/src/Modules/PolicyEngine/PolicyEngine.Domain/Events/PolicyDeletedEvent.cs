using BuildingBlocks.Domain;

namespace PolicyEngine.Domain.Events;

public sealed class PolicyDeletedEvent : DomainEvent
{
    public Guid PolicyId { get; }
    public Guid DeletedByUserId { get; }

    public PolicyDeletedEvent(Guid policyId, Guid tenantId, Guid deletedByUserId)
    {
        PolicyId = policyId;
        TenantId = tenantId;
        DeletedByUserId = deletedByUserId;
    }
}
