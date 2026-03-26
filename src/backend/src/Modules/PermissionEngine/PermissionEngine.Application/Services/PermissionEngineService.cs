using AuditLogging.Application.Services;
using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using System.Diagnostics;
using TenantManagement.Application.Services;

namespace PermissionEngine.Application.Services;

public sealed class PermissionEngineService : IPermissionEngine
{
    private readonly IEnumerable<IEvaluationStep> _steps;
    private readonly IPermissionCacheService _cache;
    private readonly IAuditLogger _auditLogger;
    private readonly ITenantService _tenantService;

    public PermissionEngineService(
        IEnumerable<IEvaluationStep> steps,
        IPermissionCacheService cache,
        IAuditLogger auditLogger,
        ITenantService tenantService)
    {
        _steps = steps.OrderBy(s => s.Order).ToList();
        _cache = cache;
        _auditLogger = auditLogger;
        _tenantService = tenantService;
    }

    public async Task<AccessResult> CanUserAccessAsync(
        Guid userId,
        string action,
        Guid resourceId,
        Guid scopeId,
        EvaluationContext context,
        CancellationToken ct = default)
    {
        // Tenant guard
        var isActive = await _tenantService.TenantIsActiveAsync(context.TenantId, ct);
        if (!isActive)
        {
            var denied = AccessResult.Denied(DenialReason.TenantSuspended, 0,
                $"Tenant {context.TenantId} is suspended.");
            await RecordDecisionAsync(userId, action, resourceId, scopeId, context, denied, ct);
            return denied;
        }

        // Cache lookup
        var cached = await _cache.GetAsync(
            userId, action, resourceId, scopeId, context.TenantId, ct);

        if (cached is not null)
        {
            await RecordDecisionAsync(userId, action, resourceId, scopeId, context, cached, ct);
            return cached;
        }

        // Pipeline execution
        var startedAt = Stopwatch.GetTimestamp();
        var request = new EvaluationRequest
        {
            UserId = userId,
            Action = action,
            ResourceId = resourceId,
            ScopeId = scopeId,
            Context = context,
            StartedAt = startedAt
        };

        AccessResult? result = null;

        foreach (var step in _steps)
        {
            result = await step.EvaluateAsync(request, ct);
            if (result is not null)
                break;
        }

        // Fallback — should never reach here as DefaultDenyStep always produces a result
        result ??= AccessResult.Denied(DenialReason.NoPermissionFound,
            Stopwatch.GetElapsedTime(startedAt).Milliseconds);

        // Cache the result with tenant-configured TTL
        var config = await _tenantService.GetConfigAsync(context.TenantId, ct);
        await _cache.SetAsync(
            userId, action, resourceId, scopeId, context.TenantId,
            result, TimeSpan.FromSeconds(config.PermissionCacheTtlSeconds), ct);

        await RecordDecisionAsync(userId, action, resourceId, scopeId, context, result, ct);

        return result;
    }

    private Task RecordDecisionAsync(
        Guid userId,
        string action,
        Guid resourceId,
        Guid scopeId,
        EvaluationContext context,
        AccessResult result,
        CancellationToken ct)
    {
        var entry = new AccessDecisionEntry(
            CorrelationId: context.CorrelationId,
            TenantId: context.TenantId,
            UserId: userId,
            Action: action,
            ResourceId: resourceId,
            ScopeId: scopeId,
            IsGranted: result.IsGranted,
            DenialReason: result.Reason?.ToString(),
            CacheHit: result.CacheHit,
            EvaluationLatencyMs: result.EvaluationLatencyMs,
            PolicyId: result.MatchedPolicyId,
            DelegationChain: result.DelegationChain is not null
                ? $"{result.DelegationChain.DelegatorId}→{result.DelegationChain.DelegateeId}"
                : null,
            Timestamp: DateTimeOffset.UtcNow);

        return _auditLogger.RecordAccessDecisionAsync(entry, ct);
    }
}
