namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by PolicyEngine module when a policy is created.
/// Effect is carried as a string ("Allow" | "Deny") to avoid referencing
/// the PolicyEngine.Domain.Entities.PolicyEffect enum cross-module.
/// Consumed cross-module by: AuditLogging, PermissionEngine (cache eviction).
/// </summary>
public sealed class PolicyCreatedEvent : DomainEvent
{
    public Guid   PolicyId        { get; }
    public string PolicyName      { get; }
    public string Effect          { get; }   // "Allow" | "Deny"
    public Guid   CreatedByUserId { get; }

    public PolicyCreatedEvent(
        Guid policyId, Guid tenantId, string policyName, string effect, Guid createdByUserId)
    {
        PolicyId        = policyId;
        TenantId        = tenantId;
        PolicyName      = policyName;
        Effect          = effect;
        CreatedByUserId = createdByUserId;
    }
}
