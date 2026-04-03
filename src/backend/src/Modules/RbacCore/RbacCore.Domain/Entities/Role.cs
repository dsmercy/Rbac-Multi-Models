using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Events;

namespace RbacCore.Domain.Entities;

public sealed class Role : AuditableEntity
{
    private readonly List<RolePermission> _permissions = new();

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }
    public IReadOnlyList<RolePermission> Permissions => _permissions.AsReadOnly();

    // EF Core constructor
    private Role() { }

    public static Role Create(
        Guid tenantId,
        string name,
        string? description,
        Guid createdByUserId,
        bool isSystemRole = false)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("INVALID_TENANT_ID", "TenantId cannot be empty.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("INVALID_ROLE_NAME", "Role name cannot be empty.");

        if (name.Length > 100)
            throw new DomainException("INVALID_ROLE_NAME", "Role name must not exceed 100 characters.");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            IsSystemRole = isSystemRole,
            CreatedBy = createdByUserId
        };

        role.AddDomainEvent(new RoleCreatedEvent(role.Id, tenantId, role.Name, createdByUserId));

        return role;
    }

    public void AddPermission(Guid permissionId, Guid grantedByUserId)
    {
        if (IsDeleted)
            throw new DomainException("ROLE_DELETED", "Cannot modify a deleted role.");

        if (_permissions.Any(p => p.PermissionId == permissionId))
            return; // Idempotent — already granted

        var rp = RolePermission.Create(Id, TenantId, permissionId, grantedByUserId);
        _permissions.Add(rp);

        AddDomainEvent(new PermissionGrantedEvent(permissionId, TenantId, Id, grantedByUserId));
    }

    public void RemovePermission(Guid permissionId, Guid revokedByUserId)
    {
        if (IsDeleted)
            throw new DomainException("ROLE_DELETED", "Cannot modify a deleted role.");

        var rp = _permissions.FirstOrDefault(p => p.PermissionId == permissionId);

        if (rp is null)
            return; // Idempotent — already removed

        _permissions.Remove(rp);

        AddDomainEvent(new PermissionRevokedEvent(permissionId, TenantId, Id, revokedByUserId));
    }

    public void Rename(string newName, string? newDescription, Guid updatedByUserId)
    {
        if (IsDeleted)
            throw new DomainException("ROLE_DELETED", "Cannot rename a deleted role.");

        if (IsSystemRole)
            throw new DomainException("SYSTEM_ROLE_IMMUTABLE", "System roles cannot be renamed.");

        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("INVALID_ROLE_NAME", "Role name cannot be empty.");

        Name = newName.Trim();
        Description = newDescription?.Trim();
        SetUpdated(updatedByUserId);
    }

    public void SoftDelete(Guid deletedByUserId)
    {
        if (IsSystemRole)
            throw new DomainException("SYSTEM_ROLE_IMMUTABLE", "System roles cannot be deleted.");

        MarkDeleted(deletedByUserId);

        AddDomainEvent(new RoleDeletedEvent(Id, TenantId, deletedByUserId));
    }
}
