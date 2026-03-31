using AuditLogging.Application.Services;
using MediatR;
using BuildingBlocks.Domain.Events;

namespace AuditLogging.Application.EventHandlers;

public sealed class PolicyCreatedAuditHandler : INotificationHandler<PolicyCreatedEvent>
{
    private readonly IAuditLogger _auditLogger;

    public PolicyCreatedAuditHandler(IAuditLogger auditLogger)
        => _auditLogger = auditLogger;

    public Task Handle(PolicyCreatedEvent notification, CancellationToken cancellationToken)
        => _auditLogger.RecordAdminActionAsync(new AdminActionEntry(
            CorrelationId: notification.EventId,
            TenantId: notification.TenantId,
            ActorUserId: notification.CreatedByUserId,
            ActionType: "policies:create",
            TargetEntityType: "Policy",
            TargetEntityId: notification.PolicyId,
            OldValue: null,
            NewValue: $"{{\"name\":\"{notification.PolicyName}\",\"effect\":\"{notification.Effect}\"}}",
            Timestamp: notification.OccurredAt), cancellationToken);
}
