using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Models;
using RbacCore.Application.Services;

namespace PermissionEngine.Application.Pipeline;

/// <summary>
/// Step 4 — Scope inheritance resolution.
/// Walks the scope hierarchy upward (project → department → org) to collect
/// all effective permissions. Writes resolved permission codes and scope IDs
/// into the request for use by later steps.
/// </summary>
public sealed class ScopeInheritanceStep : IEvaluationStep
{
    public int Order => 4;

    private readonly IRbacCoreService _rbacCoreService;

    public ScopeInheritanceStep(IRbacCoreService rbacCoreService)
        => _rbacCoreService = rbacCoreService;

    public async Task<AccessResult?> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken ct)
    {
        // Resolve the effective user for scope check:
        // If delegation is active, evaluate as the delegator's permissions
        var effectiveUserId = request.ActiveDelegation?.DelegatorId ?? request.UserId;

        // Collect all ancestor scope IDs (current scope + parents)
        var ancestorScopeIds = await _rbacCoreService.GetAncestorScopeIdsAsync(
            request.ScopeId, request.Context.TenantId, ct);

        var allScopeIds = new List<Guid> { request.ScopeId };
        allScopeIds.AddRange(ancestorScopeIds);
        request.ResolvedScopeIds = allScopeIds.AsReadOnly();

        var allPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Walk from current scope upward — more specific scopes take precedence
        // but we collect all for the RBAC step to evaluate deny-overrides-allow
        foreach (var scopeId in allScopeIds)
        {
            var perms = await _rbacCoreService.GetEffectivePermissionsAsync(
                effectiveUserId, request.Context.TenantId, scopeId, ct);

            foreach (var p in perms)
                allPermissions.Add(p.Code);
        }

        // Also include tenant-wide (null scope) permissions
        var tenantWidePerms = await _rbacCoreService.GetEffectivePermissionsAsync(
            effectiveUserId, request.Context.TenantId, null, ct);

        foreach (var p in tenantWidePerms)
            allPermissions.Add(p.Code);

        request.EffectivePermissionCodes = allPermissions.ToList().AsReadOnly();

        return null; // Continue to ABAC and RBAC steps
    }
}
