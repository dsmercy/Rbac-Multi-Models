using AuditLogging.Application.Services;
using MediatR;
using RbacCore.Domain.Events;

namespace AuditLogging.Application.EventHandlers;

public sealed class UserRoleAssignedAuditHandler : INotificationHandler<UserRoleAssignedEvent>
{
    private readonly IAuditLogger _auditLogger;

    public UserRoleAssignedAuditHandler(IAuditLogger auditLogger)
        => _auditLogger = auditLogger;

    public Task Handle(UserRoleAssignedEvent notification, CancellationToken cancellationToken)
        => _auditLogger.RecordAdminActionAsync(new AdminActionEntry(
            CorrelationId: notification.EventId,
            TenantId: notification.TenantId,
            ActorUserId: notification.AssignedByUserId,
            ActionType: "roles:assign",
            TargetEntityType: "UserRoleAssignment",
            TargetEntityId: notification.AssignmentId,
            OldValue: null,
            NewValue: $"{{\"userId\":\"{notification.UserId}\"," +
                      $"\"roleId\":\"{notification.RoleId}\"," +
                      $"\"scopeId\":\"{notification.ScopeId}\"}}",
            Timestamp: notification.OccurredAt), cancellationToken);
}
