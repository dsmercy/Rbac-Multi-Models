using BuildingBlocks.Domain;
using RbacCore.Domain.ValueObjects;

namespace RbacCore.Domain.Entities;

public sealed class Permission : AuditableEntity
{
    public Guid TenantId { get; private set; }
    public PermissionCode Code { get; private set; } = null!;
    public string? Description { get; private set; }
    public string ResourceType { get; private set; } = null!;
    public string Action { get; private set; } = null!;

    // EF Core constructor
    private Permission() { }

    public static Permission Create(
        Guid tenantId,
        string code,
        string resourceType,
        string action,
        string? description,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("INVALID_TENANT_ID", "TenantId cannot be empty.");

        if (string.IsNullOrWhiteSpace(resourceType))
            throw new DomainException("INVALID_RESOURCE_TYPE", "Resource type cannot be empty.");

        if (string.IsNullOrWhiteSpace(action))
            throw new DomainException("INVALID_ACTION", "Action cannot be empty.");

        return new Permission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = PermissionCode.Create(code),
            ResourceType = resourceType.Trim().ToLowerInvariant(),
            Action = action.Trim().ToLowerInvariant(),
            Description = description?.Trim(),
            CreatedBy = createdByUserId
        };
    }

    public void SoftDelete(Guid deletedByUserId)
        => MarkDeleted(deletedByUserId);
}
