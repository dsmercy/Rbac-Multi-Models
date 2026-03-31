using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using RbacCore.Application.Services;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// Step 4 — Scope inheritance resolution.
/// Walks the scope hierarchy upward (project → department → org) to collect
/// all effective permissions. Writes resolved permission codes and scope IDs
/// into the request for use by later steps.
///
/// Phase 5 caching:
///   • scope-tree:{tid}:{scopeId}  TTL 3600s — ancestor scope IDs per scope
///   • roles:{tid}:{uid}           TTL 300s  — final combined permission codes
///
/// Cache invalidation:
///   • scope-tree busted on ScopeUpdated event (via InvalidateTenantPermCacheAsync)
///   • roles busted on UserRoleAssigned/Revoked (via token version increment →
///     cache reads rejected on version mismatch)
/// </summary>
public sealed class ScopeInheritanceStep : IEvaluationStep
{
    public int Order => 4;

    private readonly IRbacCoreService _rbacCoreService;
    private readonly IPermissionCacheService _cache;

    public ScopeInheritanceStep(IRbacCoreService rbacCoreService, IPermissionCacheService cache)
    {
        _rbacCoreService = rbacCoreService;
        _cache           = cache;
    }

    public async Task<AccessResult?> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken ct)
    {
        var effectiveUserId = request.ActiveDelegation?.DelegatorId ?? request.UserId;

        // ── roles:{tid}:{uid} cache check ────────────────────────────────────
        // Short-circuit: if we have cached permission codes for this user, skip
        // the full scope-walk. Only valid when scopeId equals the base scope
        // the user originally resolved against (we cache tenant-wide results here).
        var cachedCodes = await _cache.GetUserPermissionCodesAsync(
            effectiveUserId, request.Context.TenantId, ct);

        if (cachedCodes is not null)
        {
            request.EffectivePermissionCodes = cachedCodes;
            // Resolve scope ancestors (still needed for deny-override checks)
            request.ResolvedScopeIds = await GetScopeAncestorsWithCacheAsync(
                request.ScopeId, request.Context.TenantId, ct);
            return null;
        }

        // ── Cache miss: full scope walk ───────────────────────────────────────
        var allScopeIds = await GetScopeAncestorsWithCacheAsync(
            request.ScopeId, request.Context.TenantId, ct);

        request.ResolvedScopeIds = allScopeIds;

        var allPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scopeId in allScopeIds)
        {
            var perms = await _rbacCoreService.GetEffectivePermissionsAsync(
                effectiveUserId, request.Context.TenantId, scopeId, ct);
            foreach (var p in perms)
                allPermissions.Add(p.Code);
        }

        // Tenant-wide (null scope) permissions
        var tenantWidePerms = await _rbacCoreService.GetEffectivePermissionsAsync(
            effectiveUserId, request.Context.TenantId, null, ct);
        foreach (var p in tenantWidePerms)
            allPermissions.Add(p.Code);

        var codes = allPermissions.ToList().AsReadOnly();
        request.EffectivePermissionCodes = codes;

        // Write to roles cache — invalidated by token version increment on role change
        await _cache.SetUserPermissionCodesAsync(
            effectiveUserId, request.Context.TenantId, codes, ct);

        return null;
    }

    /// <summary>
    /// Returns [scopeId] + all ancestor IDs, using the scope-tree cache when available.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> GetScopeAncestorsWithCacheAsync(
        Guid scopeId, Guid tenantId, CancellationToken ct)
    {
        var cached = await _cache.GetScopeAncestorsAsync(scopeId, tenantId, ct);
        if (cached is not null)
        {
            var withSelf = new List<Guid> { scopeId };
            withSelf.AddRange(cached);
            return withSelf.AsReadOnly();
        }

        var ancestors = await _rbacCoreService.GetAncestorScopeIdsAsync(scopeId, tenantId, ct);

        // Cache ancestors without self (consistent with how DB returns them)
        await _cache.SetScopeAncestorsAsync(scopeId, tenantId, ancestors, ct);

        var result = new List<Guid> { scopeId };
        result.AddRange(ancestors);
        return result.AsReadOnly();
    }
}
