namespace AuditLogging.Application.Services;

public sealed record AccessDecisionEntry(
    Guid CorrelationId,
    Guid TenantId,
    Guid UserId,
    string Action,
    Guid ResourceId,
    Guid ScopeId,
    bool IsGranted,
    string? DenialReason,
    bool CacheHit,
    long EvaluationLatencyMs,
    string? PolicyId,
    string? DelegationChain,
    DateTimeOffset Timestamp);

public sealed record AdminActionEntry(
    Guid CorrelationId,
    Guid TenantId,
    Guid ActorUserId,
    string ActionType,
    string TargetEntityType,
    Guid TargetEntityId,
    string? OldValue,
    string? NewValue,
    DateTimeOffset Timestamp);

/// <summary>
/// Public interface consumed by PermissionEngine and command handlers.
/// All writes are append-only — no modification of existing entries permitted.
/// </summary>
public interface IAuditLogger
{
    Task RecordAccessDecisionAsync(AccessDecisionEntry entry, CancellationToken ct = default);
    Task RecordAdminActionAsync(AdminActionEntry entry, CancellationToken ct = default);
}
