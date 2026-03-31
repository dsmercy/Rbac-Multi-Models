using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Events;

namespace RbacCore.Domain.Entities;

public sealed class UserRoleAssignment : AuditableEntity
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid? ScopeId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string? DeactivatedReason { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    // EF Core constructor
    private UserRoleAssignment() { }

    public static UserRoleAssignment Create(
        Guid tenantId,
        Guid userId,
        Guid roleId,
        Guid? scopeId,
        DateTimeOffset? expiresAt,
        Guid assignedByUserId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("INVALID_TENANT_ID", "TenantId cannot be empty.");

        if (userId == Guid.Empty)
            throw new DomainException("INVALID_USER_ID", "UserId cannot be empty.");

        if (roleId == Guid.Empty)
            throw new DomainException("INVALID_ROLE_ID", "RoleId cannot be empty.");

        if (expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow)
            throw new DomainException("INVALID_EXPIRY", "ExpiresAt must be in the future.");

        var assignment = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            RoleId = roleId,
            ScopeId = scopeId,
            IsActive = true,
            ExpiresAt = expiresAt,
            CreatedBy = assignedByUserId
        };

        assignment.AddDomainEvent(new UserRoleAssignedEvent(
            assignment.Id, tenantId, userId, roleId, scopeId, expiresAt, assignedByUserId));

        return assignment;
    }

    public void Deactivate(string reason, Guid deactivatedByUserId)
    {
        if (!IsActive)
            return; // Idempotent

        IsActive = false;
        DeactivatedReason = reason;
        DeactivatedAt = DateTimeOffset.UtcNow;
        SetUpdated(deactivatedByUserId);

        AddDomainEvent(new UserRoleRevokedEvent(
            Id, TenantId, UserId, RoleId, ScopeId, deactivatedByUserId));
    }

    public bool IsExpired()
        => ExpiresAt.HasValue && ExpiresAt.Value <= DateTimeOffset.UtcNow;

    public bool IsEffective()
        => IsActive && !IsExpired();
}
