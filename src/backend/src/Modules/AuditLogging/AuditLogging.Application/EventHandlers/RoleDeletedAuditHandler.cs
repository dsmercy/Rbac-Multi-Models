using AuditLogging.Application.Services;
using MediatR;
using RbacCore.Domain.Events;

namespace AuditLogging.Application.EventHandlers;

public sealed class RoleDeletedAuditHandler : INotificationHandler<RoleDeletedEvent>
{
    private readonly IAuditLogger _auditLogger;

    public RoleDeletedAuditHandler(IAuditLogger auditLogger)
        => _auditLogger = auditLogger;

    public Task Handle(RoleDeletedEvent notification, CancellationToken cancellationToken)
        => _auditLogger.RecordAdminActionAsync(new AdminActionEntry(
            CorrelationId: notification.EventId,
            TenantId: notification.TenantId,
            ActorUserId: notification.DeletedByUserId,
            ActionType: "roles:delete",
            TargetEntityType: "Role",
            TargetEntityId: notification.RoleId,
            OldValue: null,
            NewValue: null,
            Timestamp: notification.OccurredAt), cancellationToken);
}
