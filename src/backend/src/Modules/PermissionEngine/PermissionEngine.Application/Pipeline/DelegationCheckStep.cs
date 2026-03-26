using Delegation.Application.Services;
using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Models;
using RbacCore.Application.Services;
using System.Diagnostics;
using TenantManagement.Application.Services;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// Step 3 — Delegation check.
/// Validates: active delegation exists, delegator still holds the permission,
/// delegation not revoked, chain depth within tenant limit.
/// </summary>
public sealed class DelegationCheckStep : IEvaluationStep
{
    public int Order => 3;

    private readonly IDelegationService _delegationService;
    private readonly IRbacCoreService _rbacCoreService;
    private readonly ITenantService _tenantService;

    public DelegationCheckStep(
        IDelegationService delegationService,
        IRbacCoreService rbacCoreService,
        ITenantService tenantService)
    {
        _delegationService = delegationService;
        _rbacCoreService = rbacCoreService;
        _tenantService = tenantService;
    }

    public async Task<AccessResult?> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken ct)
    {
        var delegation = await _delegationService.GetActiveDelegationAsync(
            request.UserId, request.Action, request.ScopeId,
            request.Context.TenantId, ct);

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
}
