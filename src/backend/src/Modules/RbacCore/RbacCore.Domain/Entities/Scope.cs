using BuildingBlocks.Domain;

namespace RbacCore.Domain.Entities;

public enum ScopeType
{
    Organization = 1,
    Department = 2,
    Project = 3,
    Custom = 4
}

public sealed class Scope : AuditableEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public ScopeType Type { get; private set; }
    public Guid? ParentScopeId { get; private set; }

    // EF Core constructor
    private Scope() { }

    public static Scope Create(
        Guid tenantId,
        string name,
        ScopeType type,
        Guid? parentScopeId,
        string? description,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("INVALID_TENANT_ID", "TenantId cannot be empty.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("INVALID_SCOPE_NAME", "Scope name cannot be empty.");

        if (name.Length > 200)
            throw new DomainException("INVALID_SCOPE_NAME", "Scope name must not exceed 200 characters.");

        return new Scope
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Type = type,
            ParentScopeId = parentScopeId,
            CreatedBy = createdByUserId
        };
    }

    public void SoftDelete(Guid deletedByUserId)
        => MarkDeleted(deletedByUserId);
}
