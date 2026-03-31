namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by RbacCore module when a role is assigned to a user.
/// Consumed cross-module by: PermissionEngine (token version), AuditLogging.
/// </summary>
public sealed class UserRoleAssignedEvent : DomainEvent
{
    public Guid AssignmentId    { get; }
    public Guid UserId          { get; }
    public Guid RoleId          { get; }
    public Guid? ScopeId        { get; }
    public DateTimeOffset? ExpiresAt { get; }
    public Guid AssignedByUserId { get; }

    public UserRoleAssignedEvent(
        Guid assignmentId,
        Guid tenantId,
        Guid userId,
        Guid roleId,
        Guid? scopeId,
        DateTimeOffset? expiresAt,
        Guid assignedByUserId)
    {
        AssignmentId     = assignmentId;
        TenantId         = tenantId;
        UserId           = userId;
        RoleId           = roleId;
        ScopeId          = scopeId;
        ExpiresAt        = expiresAt;
        AssignedByUserId = assignedByUserId;
    }
}
