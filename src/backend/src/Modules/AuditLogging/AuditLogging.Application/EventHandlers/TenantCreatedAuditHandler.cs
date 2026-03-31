using AuditLogging.Application.Services;
using MediatR;
using BuildingBlocks.Domain.Events;

namespace AuditLogging.Application.EventHandlers;

public sealed class TenantCreatedAuditHandler : INotificationHandler<TenantCreatedEvent>
{
    private readonly IAuditLogger _auditLogger;

    public TenantCreatedAuditHandler(IAuditLogger auditLogger)
        => _auditLogger = auditLogger;

    public Task Handle(TenantCreatedEvent notification, CancellationToken cancellationToken)
        => _auditLogger.RecordAdminActionAsync(new AdminActionEntry(
            CorrelationId: notification.EventId,
            TenantId: notification.TenantId,
            ActorUserId: notification.CreatedByUserId,
            ActionType: "tenants:create",
            TargetEntityType: "Tenant",
            TargetEntityId: notification.TenantId,
            OldValue: null,
            NewValue: $"{{\"name\":\"{notification.Name}\",\"slug\":\"{notification.Slug}\"}}",
            Timestamp: notification.OccurredAt), cancellationToken);
}
