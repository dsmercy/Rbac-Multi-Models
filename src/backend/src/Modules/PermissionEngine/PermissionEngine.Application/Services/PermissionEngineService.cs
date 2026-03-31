using AuditLogging.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Exceptions;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using System.Diagnostics;
using TenantManagement.Application.Services;

namespace PermissionEngine.Application.Services;

/// <summary>
/// Orchestrates the full permission evaluation pipeline.
///
/// Phase 5 additions:
///   • 200ms evaluation timeout gate — returns Denied(EvaluationTimeout) on breach.
///   • Cache unavailability handling — CacheUnavailableException mapped to
///     Denied(RedisUnavailable). Raised only in fail-closed mode; allow-through
///     is transparent (cache returns null and evaluation falls through to DB).
///   • Stampede protection — SET NX lock on cache miss prevents concurrent
///     pile-on of pipeline executions for the same key.
/// </summary>
public sealed class PermissionEngineService : IPermissionEngine
{
    private readonly IEnumerable<IEvaluationStep> _steps;
    private readonly IPermissionCacheService _cache;
    private readonly IAuditLogger _auditLogger;
    private readonly ITenantService _tenantService;
    private readonly ILogger<PermissionEngineService> _logger;
    private readonly int _evalTimeoutMs;

    public PermissionEngineService(
        IEnumerable<IEvaluationStep> steps,
        IPermissionCacheService cache,
        IAuditLogger auditLogger,
        ITenantService tenantService,
        ILogger<PermissionEngineService> logger,
        IOptions<EvaluationOptions> options)
    {
        _steps         = steps.OrderBy(s => s.Order).ToList();
        _cache         = cache;
        _auditLogger   = auditLogger;
        _tenantService = tenantService;
        _logger        = logger;
        _evalTimeoutMs = options.Value.EvaluationTimeoutMs;
    }

    public async Task<AccessResult> CanUserAccessAsync(
        Guid userId,
        string action,
        Guid resourceId,
        Guid scopeId,
        EvaluationContext context,
        CancellationToken ct = default)
    {
        var overallSw = Stopwatch.StartNew();

        // ── Tenant guard ─────────────────────────────────────────────────────
        var isActive = await _tenantService.TenantIsActiveAsync(context.TenantId, ct);
        if (!isActive)
        {
            var denied = AccessResult.Denied(DenialReason.TenantSuspended, overallSw.ElapsedMilliseconds,
                $"Tenant {context.TenantId} is suspended.");
            await RecordDecisionAsync(userId, action, resourceId, scopeId, context, denied, ct);
            return denied;
        }

        // ── Token version check (BEFORE cache lookup) ────────────────────────
        // A stale JWT (tv=N-1) must not match a cache entry written at tv=N.
        try
        {
            if (context.TokenVersion is not null)
            {
                var currentVersion = await _cache.GetTokenVersionAsync(userId, ct);
                if (context.TokenVersion.Value != currentVersion)
                    throw new StaleTokenException(
                        $"Token version {context.TokenVersion.Value} is stale. " +
                        $"Current version is {currentVersion}. Please re-authenticate.");
            }
        }
        catch (CacheUnavailableException ex)
        {
            return await DenyOnCacheFailureAsync(ex, userId, action, resourceId, scopeId, context, overallSw.ElapsedMilliseconds, ct);
        }

        // ── Cache lookup ─────────────────────────────────────────────────────
        AccessResult? cached;
        try
        {
            cached = await _cache.GetAsync(userId, action, resourceId, scopeId, context.TenantId, ct);
        }
        catch (CacheUnavailableException ex)
        {
            return await DenyOnCacheFailureAsync(ex, userId, action, resourceId, scopeId, context, overallSw.ElapsedMilliseconds, ct);
        }

        if (cached is not null)
        {
            await RecordDecisionAsync(userId, action, resourceId, scopeId, context, cached, ct);
            return cached;
        }

        // ── Stampede protection — SET NX lock ────────────────────────────────
        // First caller past expiry acquires the lock and runs the pipeline.
        // Concurrent callers wait 100ms and retry the cache once before
        // also running the pipeline (prevents indefinite starvation).
        var lockAcquired = await _cache.TryAcquireStampedeLockAsync(
            userId, action, resourceId, scopeId, context.TenantId, ct);

        if (!lockAcquired)
        {
            await Task.Delay(100, ct);
            var retry = await _cache.GetAsync(userId, action, resourceId, scopeId, context.TenantId, ct);
            if (retry is not null)
            {
                await RecordDecisionAsync(userId, action, resourceId, scopeId, context, retry, ct);
                return retry;
            }
        }

        // ── 200ms evaluation timeout gate ────────────────────────────────────
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_evalTimeoutMs));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        AccessResult result;
        try
        {
            result = await RunPipelineAsync(userId, action, resourceId, scopeId, context, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Permission evaluation exceeded {TimeoutMs}ms for user {UserId}, action {Action}",
                _evalTimeoutMs, userId, action);
            result = AccessResult.Denied(DenialReason.EvaluationTimeout, overallSw.ElapsedMilliseconds);
            await RecordDecisionAsync(userId, action, resourceId, scopeId, context, result, ct);
            return result;
        }
        finally
        {
            if (lockAcquired)
                await _cache.ReleaseStampedeLockAsync(userId, action, resourceId, scopeId, context.TenantId);
        }

        // ── Cache the result ─────────────────────────────────────────────────
        try
        {
            var config = await _tenantService.GetConfigAsync(context.TenantId, ct);
            await _cache.SetAsync(
                userId, action, resourceId, scopeId, context.TenantId,
                result, TimeSpan.FromSeconds(config.PermissionCacheTtlSeconds), ct);
        }
        catch (CacheUnavailableException)
        {
            _logger.LogWarning(
                "Cache write skipped for user {UserId} — cache unavailable (allow-through).", userId);
        }

        await RecordDecisionAsync(userId, action, resourceId, scopeId, context, result, ct);
        return result;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<AccessResult> RunPipelineAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId,
        EvaluationContext context, CancellationToken ct)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var request = new EvaluationRequest
        {
            UserId     = userId,
            Action     = action,
            ResourceId = resourceId,
            ScopeId    = scopeId,
            Context    = context,
            StartedAt  = startedAt
        };

        AccessResult? result = null;
        foreach (var step in _steps)
        {
            result = await step.EvaluateAsync(request, ct);
            if (result is not null)
                break;
        }

        return result
            ?? AccessResult.Denied(DenialReason.NoPermissionFound,
                Stopwatch.GetElapsedTime(startedAt).Milliseconds);
    }

    private async Task<AccessResult> DenyOnCacheFailureAsync(
        CacheUnavailableException ex,
        Guid userId, string action, Guid resourceId, Guid scopeId,
        EvaluationContext context, long latencyMs, CancellationToken ct)
    {
        _logger.LogError(ex,
            "Cache unavailable (fail-closed) during permission check for user {UserId}", userId);

        var denied = AccessResult.Denied(DenialReason.RedisUnavailable, latencyMs);
        await RecordDecisionAsync(userId, action, resourceId, scopeId, context, denied, ct);
        return denied;
    }

    private Task RecordDecisionAsync(
        Guid userId, string action, Guid resourceId, Guid scopeId,
        EvaluationContext context, AccessResult result, CancellationToken ct)
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
