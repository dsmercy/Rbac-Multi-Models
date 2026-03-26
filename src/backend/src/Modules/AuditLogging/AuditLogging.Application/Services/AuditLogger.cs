using AuditLogging.Domain.Entities;
using AuditLogging.Domain.Interfaces;

namespace AuditLogging.Application.Services;

public sealed class AuditLogger : IAuditLogger
{
    private readonly IAuditLogRepository _repository;

    public AuditLogger(IAuditLogRepository repository)
        => _repository = repository;

    public Task RecordAccessDecisionAsync(
        AccessDecisionEntry entry, CancellationToken ct = default)
    {
        var log = AuditLog.CreateAccessDecision(
            entry.TenantId,
            entry.CorrelationId,
            entry.UserId,
            entry.Action,
            entry.ResourceId,
            entry.ScopeId,
            entry.IsGranted,
            entry.DenialReason,
            entry.CacheHit,
            entry.EvaluationLatencyMs,
            entry.PolicyId,
            entry.DelegationChain);

        return _repository.AppendAsync(log, ct);
    }

    public Task RecordAdminActionAsync(
        AdminActionEntry entry, CancellationToken ct = default)
    {
        var log = AuditLog.CreateAdminAction(
            entry.TenantId,
            entry.CorrelationId,
            entry.ActorUserId,
            entry.ActionType,
            entry.TargetEntityType,
            entry.TargetEntityId,
            entry.OldValue,
            entry.NewValue);

        return _repository.AppendAsync(log, ct);
    }
}
