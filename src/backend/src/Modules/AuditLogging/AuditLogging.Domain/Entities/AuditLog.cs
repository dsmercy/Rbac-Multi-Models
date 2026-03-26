using BuildingBlocks.Domain;

namespace AuditLogging.Domain.Entities;

public enum AuditLogType
{
    AccessDecision = 1,
    AdminAction = 2
}

/// <summary>
/// Immutable append-only audit log entry.
/// No Update or Delete operations are permitted on this entity.
/// All properties are set at construction time only.
/// </summary>
public sealed class AuditLog : Entity
{
    public Guid TenantId { get; private init; }
    public AuditLogType LogType { get; private init; }
    public Guid CorrelationId { get; private init; }
    public Guid ActorUserId { get; private init; }
    public string Action { get; private init; } = null!;
    public Guid? ResourceId { get; private init; }
    public Guid? ScopeId { get; private init; }
    public bool? IsGranted { get; private init; }
    public string? DenialReason { get; private init; }
    public bool? CacheHit { get; private init; }
    public long? EvaluationLatencyMs { get; private init; }
    public string? PolicyId { get; private init; }
    public string? DelegationChain { get; private init; }
    public string? TargetEntityType { get; private init; }
    public Guid? TargetEntityId { get; private init; }
    public string? OldValue { get; private init; }
    public string? NewValue { get; private init; }
    public bool IsPlatformAction { get; private init; }
    public DateTimeOffset Timestamp { get; private init; }

    // EF Core constructor
    private AuditLog() { }

    public static AuditLog CreateAccessDecision(
        Guid tenantId,
        Guid correlationId,
        Guid actorUserId,
        string action,
        Guid resourceId,
        Guid scopeId,
        bool isGranted,
        string? denialReason,
        bool cacheHit,
        long evaluationLatencyMs,
        string? policyId,
        string? delegationChain,
        bool isPlatformAction = false)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LogType = AuditLogType.AccessDecision,
            CorrelationId = correlationId,
            ActorUserId = actorUserId,
            Action = action,
            ResourceId = resourceId,
            ScopeId = scopeId,
            IsGranted = isGranted,
            DenialReason = denialReason,
            CacheHit = cacheHit,
            EvaluationLatencyMs = evaluationLatencyMs,
            PolicyId = policyId,
            DelegationChain = delegationChain,
            IsPlatformAction = isPlatformAction,
            Timestamp = DateTimeOffset.UtcNow
        };

    public static AuditLog CreateAdminAction(
        Guid tenantId,
        Guid correlationId,
        Guid actorUserId,
        string action,
        string targetEntityType,
        Guid targetEntityId,
        string? oldValue,
        string? newValue,
        bool isPlatformAction = false)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LogType = AuditLogType.AdminAction,
            CorrelationId = correlationId,
            ActorUserId = actorUserId,
            Action = action,
            TargetEntityType = targetEntityType,
            TargetEntityId = targetEntityId,
            OldValue = oldValue,
            NewValue = newValue,
            IsPlatformAction = isPlatformAction,
            Timestamp = DateTimeOffset.UtcNow
        };
}
