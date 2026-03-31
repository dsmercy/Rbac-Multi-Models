using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Events;

namespace PolicyEngine.Domain.Entities;

public sealed class Policy : AuditableEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public PolicyEffect Effect { get; private set; }
    public string ConditionTreeJson { get; private set; } = null!;

    /// <summary>
    /// Optional: scopes this policy to a specific resource ID.
    /// Null means the policy applies globally across the tenant.
    /// </summary>
    public Guid? ResourceId { get; private set; }

    /// <summary>
    /// Optional: scopes this policy to a specific action (e.g. "users:delete").
    /// Null means the policy applies to all actions.
    /// </summary>
    public string? Action { get; private set; }

    public bool IsActive { get; private set; }

    // EF Core constructor
    private Policy() { }

    public static Policy Create(
        Guid tenantId,
        string name,
        string? description,
        PolicyEffect effect,
        string conditionTreeJson,
        Guid? resourceId,
        string? action,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("INVALID_TENANT_ID", "TenantId cannot be empty.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("INVALID_POLICY_NAME", "Policy name cannot be empty.");

        if (string.IsNullOrWhiteSpace(conditionTreeJson))
            throw new DomainException("INVALID_CONDITION_TREE", "ConditionTreeJson cannot be empty.");

        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Effect = effect,
            ConditionTreeJson = conditionTreeJson,
            ResourceId = resourceId,
            Action = action?.Trim().ToLowerInvariant(),
            IsActive = true,
            CreatedBy = createdByUserId
        };

        policy.AddDomainEvent(new PolicyCreatedEvent(
            policy.Id, tenantId, name, effect.ToString(), createdByUserId));

        return policy;
    }

    public void Update(
        string name,
        string? description,
        string conditionTreeJson,
        string? action,
        Guid updatedByUserId)
    {
        if (IsDeleted)
            throw new DomainException("POLICY_DELETED", "Cannot update a deleted policy.");

        Name = name.Trim();
        Description = description?.Trim();
        ConditionTreeJson = conditionTreeJson;
        Action = action?.Trim().ToLowerInvariant();
        SetUpdated(updatedByUserId);

        AddDomainEvent(new PolicyUpdatedEvent(Id, TenantId, updatedByUserId));
    }

    public void Deactivate(Guid updatedByUserId)
    {
        IsActive = false;
        SetUpdated(updatedByUserId);
    }

    public void SoftDelete(Guid deletedByUserId)
    {
        MarkDeleted(deletedByUserId);
        AddDomainEvent(new PolicyDeletedEvent(Id, TenantId, deletedByUserId));
    }
}
