using AuditLogging.Application.Services;
using Identity.Domain.Events;
using MediatR;

namespace AuditLogging.Application.EventHandlers;

public sealed class UserCreatedAuditHandler : INotificationHandler<UserCreatedEvent>
{
    private readonly IAuditLogger _auditLogger;

    public UserCreatedAuditHandler(IAuditLogger auditLogger)
        => _auditLogger = auditLogger;

    public Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
        => _auditLogger.RecordAdminActionAsync(new AdminActionEntry(
            CorrelationId: notification.EventId,
            TenantId: notification.TenantId,
            ActorUserId: notification.UserId,
            ActionType: "users:create",
            TargetEntityType: "User",
            TargetEntityId: notification.UserId,
            OldValue: null,
            NewValue: $"{{\"email\":\"{notification.Email}\"}}",
            Timestamp: notification.OccurredAt), cancellationToken);
}
