using BuildingBlocks.Domain;

namespace PolicyEngine.Domain.Events;

public sealed class PolicyUpdatedEvent : DomainEvent
{
    public Guid PolicyId { get; }
    public Guid UpdatedByUserId { get; }

    public PolicyUpdatedEvent(Guid policyId, Guid tenantId, Guid updatedByUserId)
    {
        PolicyId = policyId;
        TenantId = tenantId;
        UpdatedByUserId = updatedByUserId;
    }
}
