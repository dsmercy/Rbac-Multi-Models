using BuildingBlocks.Domain;

namespace RbacCore.Domain.Events;

public sealed class UserRoleRevokedEvent : DomainEvent
{
    public Guid AssignmentId { get; }
    public Guid UserId { get; }
    public Guid RoleId { get; }
    public Guid? ScopeId { get; }
    public Guid RevokedByUserId { get; }

    public UserRoleRevokedEvent(
        Guid assignmentId,
        Guid tenantId,
        Guid userId,
        Guid roleId,
        Guid? scopeId,
        Guid revokedByUserId)
    {
        AssignmentId = assignmentId;
        TenantId = tenantId;
        UserId = userId;
        RoleId = roleId;
        ScopeId = scopeId;
        RevokedByUserId = revokedByUserId;
    }
}
