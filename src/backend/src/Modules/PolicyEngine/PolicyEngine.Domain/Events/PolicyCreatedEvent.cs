using BuildingBlocks.Domain;
using PolicyEngine.Domain.Entities;

namespace PolicyEngine.Domain.Events;

public sealed class PolicyCreatedEvent : DomainEvent
{
    public Guid PolicyId { get; }
    public string PolicyName { get; }
    public PolicyEffect Effect { get; }
    public Guid CreatedByUserId { get; }

    public PolicyCreatedEvent(
        Guid policyId,
        Guid tenantId,
        string policyName,
        PolicyEffect effect,
        Guid createdByUserId)
    {
        PolicyId = policyId;
        TenantId = tenantId;
        PolicyName = policyName;
        Effect = effect;
        CreatedByUserId = createdByUserId;
    }
}
