using AuditLogging.Application.Services;
using BuildingBlocks.Domain.Events;
using MediatR;

namespace AuditLogging.Application.EventHandlers;

public sealed class DelegationRevokedAuditHandler : INotificationHandler<DelegationRevokedEvent>
{
    private readonly IAuditLogger _auditLogger;

    public DelegationRevokedAuditHandler(IAuditLogger auditLogger)
        => _auditLogger = auditLogger;

    public Task Handle(DelegationRevokedEvent notification, CancellationToken cancellationToken)
        => _auditLogger.RecordAdminActionAsync(new AdminActionEntry(
            CorrelationId: notification.EventId,
            TenantId: notification.TenantId,
            ActorUserId: notification.RevokedByUserId,
            ActionType: "delegations:revoke",
            TargetEntityType: "Delegation",
            TargetEntityId: notification.DelegationId,
            OldValue: null,
            NewValue: $"{{\"delegateeId\":\"{notification.DelegateeId}\"}}",
            Timestamp: notification.OccurredAt), cancellationToken);
}
