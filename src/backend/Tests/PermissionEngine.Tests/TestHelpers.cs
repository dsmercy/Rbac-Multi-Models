// =============================================================================
//  TestHelpers.cs
//  Shared test infrastructure consumed by every test class.
//
//  MockDependencies   – builds NSubstitute fakes with safe defaults, exposes
//                       convenience helpers for common setup patterns, and
//                       assembles a fully-wired PermissionEngineService.
//
//  ContextBuilder     – fluent builder for EvaluationContext.
//
//  TestIds            – stable GUIDs / strings reused across all tests so
//                       assertion messages are human-readable.
// =============================================================================

using AuditLogging.Application.Services;
using Delegation.Application.Common;
using Delegation.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PermissionEngine.Application;
using PermissionEngine.Application.Pipeline;
using PermissionEngine.Application.Services;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using PolicyEngine.Application.Services;
using RbacCore.Application.Common;
using RbacCore.Application.Services;
using TenantManagement.Application.Common;
using TenantManagement.Application.Services;
using TenantManagement.Domain.ValueObjects;

namespace PermissionEngine.Tests;

// ─────────────────────────────────────────────────────────────────────────────
//  Stable IDs shared by every test
// ─────────────────────────────────────────────────────────────────────────────
public static class TestIds
{
    public static readonly Guid TenantId        = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid UserId          = new("20000000-0000-0000-0000-000000000001");
    public static readonly Guid ResourceId      = new("30000000-0000-0000-0000-000000000001");
    public static readonly Guid ScopeId         = new("40000000-0000-0000-0000-000000000001");
    public static readonly Guid ParentScopeId   = new("40000000-0000-0000-0000-000000000002");
    public static readonly Guid GrandparentScopeId = new("40000000-0000-0000-0000-000000000003");
    public static readonly Guid DelegatorId     = new("20000000-0000-0000-0000-000000000002");
    public static readonly Guid DelegationId    = new("50000000-0000-0000-0000-000000000001");
    public static readonly Guid RoleId          = new("60000000-0000-0000-0000-000000000001");
    public static readonly Guid PermissionId    = new("70000000-0000-0000-0000-000000000001");
    public static readonly Guid ForeignTenantId = new("10000000-0000-0000-0000-000000000099");

    public const string Action         = "users:read";
    public const string DeleteAction   = "users:delete";
    public const string PolicyAction   = "policies:create";
    public const string AuditAction    = "audit-logs:read";
    public const string UnknownAction  = "unknown:action";
}

// ─────────────────────────────────────────────────────────────────────────────
//  Default tenant configuration
// ─────────────────────────────────────────────────────────────────────────────
public static class DefaultTenantConfig
{
    public static TenantConfigDto Active => new(
        MaxDelegationChainDepth:      1,
        PermissionCacheTtlSeconds:    60,
        TokenVersionCacheTtlSeconds:  3600,
        MaxUsersAllowed:              500,
        MaxRolesAllowed:              100);
}

// ─────────────────────────────────────────────────────────────────────────────
//  Fluent EvaluationContext builder
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ContextBuilder
{
    private Guid   _tenantId    = TestIds.TenantId;
    private int?   _tokenVersion;
    private bool   _superAdmin;
    private readonly Dictionary<string, object> _userAttrs = new();
    private readonly Dictionary<string, object> _envAttrs  = new();
    private readonly Dictionary<string, object> _resAttrs  = new();

    public ContextBuilder WithTenant(Guid id)          { _tenantId     = id;    return this; }
    public ContextBuilder WithTokenVersion(int v)      { _tokenVersion = v;     return this; }
    public ContextBuilder AsSuperAdmin()               { _superAdmin   = true;  return this; }
    public ContextBuilder WithUserAttr(string k, object v) { _userAttrs[k] = v; return this; }
    public ContextBuilder WithEnvAttr(string k, object v)  { _envAttrs[k]  = v; return this; }
    public ContextBuilder WithResAttr(string k, object v)  { _resAttrs[k]  = v; return this; }

    public EvaluationContext Build() => new EvaluationContext(
    tenantId: _tenantId,
    correlationId: Guid.NewGuid(),
    userAttributes: _userAttrs,
    resourceAttributes: _resAttrs,
    environmentAttributes: _envAttrs,
    tokenVersion: _tokenVersion);

}

// ─────────────────────────────────────────────────────────────────────────────
//  MockDependencies
//  Central factory: creates all NSubstitute fakes with safe defaults and
//  exposes helpers that individual tests use to change behaviour.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class MockDependencies
{
    // ── Public fakes ─────────────────────────────────────────────────────────
    public IPermissionCacheService Cache        { get; } = Substitute.For<IPermissionCacheService>();
    public IAuditLogger            AuditLogger  { get; } = Substitute.For<IAuditLogger>();
    public ITenantService          TenantSvc    { get; } = Substitute.For<ITenantService>();
    public IRbacCoreService        RbacSvc      { get; } = Substitute.For<IRbacCoreService>();
    public IPolicyEngine           PolicyEngine { get; } = Substitute.For<IPolicyEngine>();
    public IDelegationService      DelegSvc     { get; } = Substitute.For<IDelegationService>();

    // ── Constructor – install safe defaults ──────────────────────────────────
    public MockDependencies()
    {
        // Tenant: active with default config
        TenantSvc.TenantIsActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns(true);
        TenantSvc.GetConfigAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns(DefaultTenantConfig.Active);

        // Cache: miss by default
        Cache.GetAsync(
                Arg.Any<Guid>(), Arg.Any<string>(),
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
             .Returns((AccessResult?)null);

        // Token version: 0 (current)
        Cache.GetTokenVersionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns(0);

        // All policy evaluators: not-applicable by default
        PolicyEngine.EvaluateGlobalPoliciesAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(),
                Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>())
             .Returns(new PolicyEvalResult(PolicyDecision.NotApplicable, null, null));

        PolicyEngine.EvaluateResourcePoliciesAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>())
             .Returns(new PolicyEvalResult(PolicyDecision.NotApplicable, null, null));

        PolicyEngine.EvaluatePoliciesAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>())
             .Returns(new PolicyEvalResult(PolicyDecision.NotApplicable, null, null));

        // No delegation by default
        DelegSvc.GetActiveDelegationAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
             .Returns((ActiveDelegationDto?)null);

        // No ancestor scopes
        RbacSvc.GetAncestorScopeIdsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
               .Returns((IReadOnlyList<Guid>)Array.Empty<Guid>());

        // ScopeInheritanceStep + DelegationCheckStep cache: miss by default
        Cache.GetUserPermissionCodesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((IReadOnlyList<string>?)null);
        Cache.GetScopeAncestorsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((IReadOnlyList<Guid>?)null);
        Cache.GetDelegationJsonAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((string?)null);

        // No effective permissions
        RbacSvc.GetEffectivePermissionsAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
             .Returns((IReadOnlyList<PermissionDto>)Array.Empty<PermissionDto>());

        // UserHasPermission: false by default
        RbacSvc.UserHasPermissionAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(),
                Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
             .Returns(false);

        // Audit logger is fire-and-forget
        AuditLogger.RecordAccessDecisionAsync(
                Arg.Any<AccessDecisionEntry>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
    }

    // ── Service factory ───────────────────────────────────────────────────────
    /// <summary>Builds a fully-wired PermissionEngineService from these fakes.</summary>
    public PermissionEngineService BuildService()
    {
        var steps = new IEvaluationStep[]
        {
            new TokenVersionValidationStep(Cache),
            new GlobalDenyStep(PolicyEngine),
            new ResourceLevelOverrideStep(PolicyEngine),
            new DelegationCheckStep(DelegSvc, RbacSvc, TenantSvc, Cache),
            new ScopeInheritanceStep(RbacSvc, Cache),
            new AbacPolicyStep(PolicyEngine),
            new RbacPermissionCheckStep(),
            new DefaultDenyStep(),
        };

        return new PermissionEngineService(steps, Cache, AuditLogger, TenantSvc,
            NullLogger<PermissionEngineService>.Instance,
            Options.Create(new EvaluationOptions()));
    }

    // ── Setup helpers ─────────────────────────────────────────────────────────

    /// <summary>Grant the specified user a single permission at the given scope.</summary>
    public void GrantPermission(Guid userId, Guid tenantId, Guid? scopeId, string action)
    {
        var dto  = new PermissionDto(TestIds.PermissionId, tenantId, action, "resource", action, null);
        var list = (IReadOnlyList<PermissionDto>)new List<PermissionDto> { dto };
        RbacSvc.GetEffectivePermissionsAsync(userId, tenantId, scopeId, Arg.Any<CancellationToken>())
               .Returns(list);
    }

    /// <summary>Grant using default TestIds (userId, tenantId, scopeId, action).</summary>
    public void GrantDefaultPermission()
        => GrantPermission(TestIds.UserId, TestIds.TenantId, TestIds.ScopeId, TestIds.Action);

    /// <summary>Set up an active delegation from DelegatorId → UserId.</summary>
    public ActiveDelegationDto SetupActiveDelegation(
        string action      = TestIds.Action,
        int    chainDepth  = 1,
        DateTimeOffset? expiresAt = null)
    {
        var dto = new ActiveDelegationDto(
            Id:              TestIds.DelegationId,
            TenantId:        TestIds.TenantId,
            DelegatorId:     TestIds.DelegatorId,
            DelegateeId:     TestIds.UserId,
            PermissionCodes: new[] { action },
            ScopeId:         TestIds.ScopeId,
            ExpiresAt:       expiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
            ChainDepth:      chainDepth,
            CreatedAt:       DateTimeOffset.UtcNow.AddMinutes(-5));

        DelegSvc.GetActiveDelegationAsync(
                TestIds.UserId, action, TestIds.ScopeId, TestIds.TenantId,
                Arg.Any<CancellationToken>())
             .Returns(dto);

        return dto;
    }

    /// <summary>Make delegator hold the delegated permission (so delegation chain is valid).</summary>
    public void DelegatorHoldsPermission(string action = TestIds.Action)
    {
        RbacSvc.UserHasPermissionAsync(
                TestIds.DelegatorId, action, TestIds.TenantId,
                TestIds.ScopeId, Arg.Any<CancellationToken>())
             .Returns(true);

        GrantPermission(TestIds.DelegatorId, TestIds.TenantId, TestIds.ScopeId, action);
    }

    /// <summary>Place a specific AccessResult into the cache for the default IDs.</summary>
    public void SetCacheHit(AccessResult result)
    {
        Cache.GetAsync(
                TestIds.UserId, TestIds.Action, TestIds.ResourceId,
                TestIds.ScopeId, TestIds.TenantId, Arg.Any<CancellationToken>())
             .Returns(result);
    }

    /// <summary>Set the current Redis token version for the default user.</summary>
    public void SetTokenVersion(int version)
        => Cache.GetTokenVersionAsync(TestIds.UserId, Arg.Any<CancellationToken>()).Returns(version);

    /// <summary>Configure a global-deny policy result.</summary>
    public void SetGlobalDeny(string policyId = "global-deny-pol")
        => PolicyEngine.EvaluateGlobalPoliciesAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(),
                Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>())
             .Returns(new PolicyEvalResult(PolicyDecision.Deny, policyId, "Unconditional deny"));

    /// <summary>Configure a resource-level-deny policy result.</summary>
    public void SetResourceLevelDeny(string policyId = "resource-deny-pol")
        => PolicyEngine.EvaluateResourcePoliciesAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>())
             .Returns(new PolicyEvalResult(PolicyDecision.Deny, policyId, "Resource locked"));

    /// <summary>Configure a resource-level-allow policy result (pipeline continues).</summary>
    public void SetResourceLevelAllow(string policyId = "resource-allow-pol")
        => PolicyEngine.EvaluateResourcePoliciesAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>())
             .Returns(new PolicyEvalResult(PolicyDecision.Allow, policyId, null));

    /// <summary>Configure an ABAC-deny policy result.</summary>
    public void SetAbacDeny(string policyId = "abac-deny-pol")
        => PolicyEngine.EvaluatePoliciesAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>())
             .Returns(new PolicyEvalResult(PolicyDecision.Deny, policyId, "Condition failed"));

    /// <summary>Configure an ABAC-allow policy result.</summary>
    public void SetAbacAllow(string policyId = "abac-allow-pol")
        => PolicyEngine.EvaluatePoliciesAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>())
             .Returns(new PolicyEvalResult(PolicyDecision.Allow, policyId, null));

    /// <summary>Set parent scope ancestors for default ScopeId.</summary>
    public void SetAncestorScopes(params Guid[] ancestorIds)
        => RbacSvc.GetAncestorScopeIdsAsync(TestIds.ScopeId, TestIds.TenantId, Arg.Any<CancellationToken>())
                  .Returns((IReadOnlyList<Guid>)ancestorIds.ToList());

    /// <summary>Grant a permission at a specific (non-default) scope.</summary>
    public void GrantPermissionAtScope(Guid scopeId, string action = TestIds.Action)
    {
        var dto  = new PermissionDto(Guid.NewGuid(), TestIds.TenantId, action, "resource", action, null);
        var list = (IReadOnlyList<PermissionDto>)new List<PermissionDto> { dto };
        RbacSvc.GetEffectivePermissionsAsync(
                TestIds.UserId, TestIds.TenantId, scopeId, Arg.Any<CancellationToken>())
             .Returns(list);
    }

    /// <summary>Suspend the default tenant.</summary>
    public void SuspendTenant()
        => TenantSvc.TenantIsActiveAsync(TestIds.TenantId, Arg.Any<CancellationToken>()).Returns(false);

    /// <summary>Suspend a specific tenant.</summary>
    public void SuspendTenant(Guid tenantId)
        => TenantSvc.TenantIsActiveAsync(tenantId, Arg.Any<CancellationToken>()).Returns(false);

    /// <summary>Increase max delegation chain depth for the default tenant.</summary>
    public void SetMaxDelegationChainDepth(int depth)
        => TenantSvc.GetConfigAsync(TestIds.TenantId, Arg.Any<CancellationToken>())
                    .Returns(new TenantConfigDto(depth, 60, 3600, 500, 100));
}
