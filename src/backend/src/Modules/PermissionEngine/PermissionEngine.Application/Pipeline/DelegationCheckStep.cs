using Delegation.Application.Common;
using Delegation.Application.Services;
using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using RbacCore.Application.Services;
using System.Diagnostics;
using System.Text.Json;
using TenantManagement.Application.Services;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// Step 3 — Delegation check.
/// Validates: active delegation exists, delegator still holds the permission,
/// delegation not revoked, chain depth within tenant limit.
///
/// Cache-aside for delegation:{tid}:{uid}  TTL 60s:
///   • On hit:  deserialize and use cached ActiveDelegationDto.
///   • On miss: query DB via IDelegationService, write result to cache.
///   • Expired/invalid delegations are NOT cached — force re-evaluation next request.
/// </summary>
public sealed class DelegationCheckStep : IEvaluationStep
{
    public int Order => 3;

    private readonly IDelegationService _delegationService;
    private readonly IRbacCoreService _rbacCoreService;
    private readonly ITenantService _tenantService;
    private readonly IPermissionCacheService _cache;

    public DelegationCheckStep(
        IDelegationService delegationService,
        IRbacCoreService rbacCoreService,
        ITenantService tenantService,
        IPermissionCacheService cache)
    {
        _delegationService = delegationService;
        _rbacCoreService   = rbacCoreService;
        _tenantService     = tenantService;
        _cache             = cache;
    }

    public async Task<AccessResult?> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken ct)
    {
        var delegation = await GetDelegationAsync(request.UserId, request.Action,
            request.ScopeId, request.Context.TenantId, ct);

        if (delegation is null)
            return null; // No delegation — continue pipeline

        var latency = Stopwatch.GetElapsedTime(request.StartedAt).Milliseconds;

        // (a) Check expiry at evaluation time — not a background job
        if (delegation.ExpiresAt <= DateTimeOffset.UtcNow)
            return AccessResult.Denied(DenialReason.DelegationExpired, latency,
                $"Delegation {delegation.Id} has expired.");

        // (b) Validate delegator still holds the permission
        var delegatorStillHolds = await _rbacCoreService.UserHasPermissionAsync(
            delegation.DelegatorId, request.Action,
            request.Context.TenantId, request.ScopeId, ct);

        if (!delegatorStillHolds)
            return AccessResult.Denied(DenialReason.DelegatorLostPermission, latency,
                $"Delegator {delegation.DelegatorId} no longer holds permission '{request.Action}'.");

        // (c) Chain depth within tenant-configured max
        var tenantConfig = await _tenantService.GetConfigAsync(request.Context.TenantId, ct);

        if (delegation.ChainDepth > tenantConfig.MaxDelegationChainDepth)
            return AccessResult.Denied(DenialReason.DelegationChainTooDeep, latency,
                $"Delegation chain depth {delegation.ChainDepth} exceeds max {tenantConfig.MaxDelegationChainDepth}.");

        // Delegation is valid — record it and continue pipeline as delegator
        request.ActiveDelegation = new DelegationChainInfo(
            delegation.Id,
            delegation.DelegatorId,
            request.UserId,
            delegation.ChainDepth);

        return null; // Continue pipeline
    }

    /// <summary>
    /// Cache-aside for active delegations. Tries cache first; on miss queries DB and writes cache.
    /// </summary>
    private async Task<ActiveDelegationDto?> GetDelegationAsync(
        Guid userId, string action, Guid scopeId, Guid tenantId, CancellationToken ct)
    {
        var json = await _cache.GetDelegationJsonAsync(userId, tenantId, ct);

        if (json is not null)
        {
            var cached = JsonSerializer.Deserialize<ActiveDelegationDto>(json);
            // Only use cached delegation if it covers the requested action
            if (cached is not null && cached.PermissionCodes.Contains(action, StringComparer.OrdinalIgnoreCase))
                return cached;
        }

        var delegation = await _delegationService.GetActiveDelegationAsync(
            userId, action, scopeId, tenantId, ct);

        if (delegation is not null)
            await _cache.SetDelegationJsonAsync(userId, tenantId,
                JsonSerializer.Serialize(delegation), ct);

        return delegation;
    }
}
