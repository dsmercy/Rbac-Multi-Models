// =============================================================================
//  PermissionEngineServiceTests.cs  –  Pipeline branch coverage
//  30 test cases, one per logical path through CanUserAccess.
// =============================================================================

using AuditLogging.Application.Services;
using FluentAssertions;
using NSubstitute;
using PermissionEngine.Domain.Models;
using PolicyEngine.Application.Services;
using RbacCore.Application.Common;
using Xunit;

namespace PermissionEngine.Tests.Pipeline;

public sealed class PermissionEngineServiceTests
{
    // ── helpers ───────────────────────────────────────────────────────────────
    private static (MockDependencies m, EvaluationContext ctx) Default()
        => (new MockDependencies(), new ContextBuilder().Build());

    // =========================================================================
    //  CACHE LAYER (Tests 01–03)
    // =========================================================================

    [Fact(DisplayName = "T01 – cache hit: granted result returned without pipeline")]
    public async Task T01_CacheHit_Granted_SkipsPipeline()
    {
        var (m, ctx) = Default();
        m.SetCacheHit(AccessResult.GrantedFromCache());

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
        result.CacheHit.Should().BeTrue();
        await m.PolicyEngine.DidNotReceive().EvaluateGlobalPoliciesAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(),
            Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "T02 – cache hit: denied result returned without pipeline")]
    public async Task T02_CacheHit_Denied_SkipsPipeline()
    {
        var (m, ctx) = Default();
        m.SetCacheHit(AccessResult.DeniedFromCache(DenialReason.NoPermissionFound));

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.NoPermissionFound);
        result.CacheHit.Should().BeTrue();
    }

    [Fact(DisplayName = "T03 – live evaluation result is written to cache")]
    public async Task T03_LiveEval_ResultWrittenToCache()
    {
        var (m, _) = Default();
        m.GrantDefaultPermission();
        var ctx = new ContextBuilder().Build();

        await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        await m.Cache.Received(1).SetAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, TestIds.TenantId,
            Arg.Is<AccessResult>(r => r.IsGranted),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    //  TENANT GUARD (Tests 04–05)
    // =========================================================================

    [Fact(DisplayName = "T04 – suspended tenant: denied before any pipeline step")]
    public async Task T04_TenantSuspended_DeniedImmediately()
    {
        var (m, ctx) = Default();
        m.SuspendTenant();

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.TenantSuspended);
        await m.PolicyEngine.DidNotReceive().EvaluateGlobalPoliciesAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(),
            Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "T05 – active tenant: pipeline continues past tenant guard")]
    public async Task T05_ActiveTenant_PipelineContinues()
    {
        var (m, ctx) = Default();
        m.GrantDefaultPermission();

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
    }

    // =========================================================================
    //  STEP 0 – TOKEN VERSION (Tests 06–08)
    // =========================================================================

    [Fact(DisplayName = "T06 – stale token version: denied before any DB or policy touch")]
    public async Task T06_StaleTokenVersion_DeniedAtStep0()
    {
        var m = new MockDependencies();
        m.SetTokenVersion(5);                           // Redis says 5
        var ctx = new ContextBuilder().WithTokenVersion(2).Build(); // JWT says 2 → stale

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.TokenVersionMismatch);
        await m.RbacSvc.DidNotReceive().GetEffectivePermissionsAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "T07 – matching token version: pipeline continues")]
    public async Task T07_MatchingTokenVersion_PipelineContinues()
    {
        var m = new MockDependencies();
        m.SetTokenVersion(3);
        m.GrantDefaultPermission();
        var ctx = new ContextBuilder().WithTokenVersion(3).Build();

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
    }

    [Fact(DisplayName = "T08 – null token version (server-to-server): step 0 skipped")]
    public async Task T08_NullTokenVersion_Step0Skipped()
    {
        var m = new MockDependencies();
        m.SetTokenVersion(99);   // Redis version is different — irrelevant when tv=null
        m.GrantDefaultPermission();
        var ctx = new EvaluationContext(TestIds.TenantId, Guid.NewGuid()); // no tokenVersion

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
    }

    // =========================================================================
    //  STEP 1 – GLOBAL DENY (Tests 09–10)
    // =========================================================================

    [Fact(DisplayName = "T09 – global deny policy: short-circuits before step 2")]
    public async Task T09_GlobalDenyPolicy_ShortCircuits()
    {
        var (m, ctx) = Default();
        m.SetGlobalDeny("global-ip-block");

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.ExplicitGlobalDeny);
        result.MatchedPolicyId.Should().Be("global-ip-block");

        await m.PolicyEngine.DidNotReceive().EvaluateResourcePoliciesAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "T10 – global allow policy: pipeline continues to step 2")]
    public async Task T10_GlobalAllowPolicy_PipelineContinues()
    {
        var (m, ctx) = Default();
        // Global allow is NotApplicable by default — user still needs RBAC
        m.GrantDefaultPermission();

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
        await m.PolicyEngine.Received().EvaluateResourcePoliciesAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<EvaluationContext>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    //  STEP 2 – RESOURCE-LEVEL OVERRIDE (Tests 11–12)
    // =========================================================================

    [Fact(DisplayName = "T11 – resource-level deny: short-circuits")]
    public async Task T11_ResourceLevelDeny_ShortCircuits()
    {
        var (m, ctx) = Default();
        m.SetResourceLevelDeny("resource-locked");

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.ResourceLevelDeny);
        result.MatchedPolicyId.Should().Be("resource-locked");
    }

    [Fact(DisplayName = "T12 – resource-level allow: pipeline continues through delegation")]
    public async Task T12_ResourceLevelAllow_PipelineContinues()
    {
        var (m, ctx) = Default();
        m.SetResourceLevelAllow();
        m.GrantDefaultPermission();

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
    }

    // =========================================================================
    //  STEP 3 – DELEGATION CHECK (Tests 13–17)
    // =========================================================================

    [Fact(DisplayName = "T13 – delegation expired: denied with DelegationExpired")]
    public async Task T13_DelegationExpired_Denied()
    {
        var (m, ctx) = Default();
        m.SetupActiveDelegation(expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1));

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.DelegationExpired);
    }

    [Fact(DisplayName = "T14 – delegator lost permission: denied with DelegatorLostPermission")]
    public async Task T14_DelegatorLostPermission_Denied()
    {
        var (m, ctx) = Default();
        m.SetupActiveDelegation();
        // DelegatorHoldsPermission NOT called → RbacSvc returns false (default)

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.DelegatorLostPermission);
    }

    [Fact(DisplayName = "T15 – delegation chain too deep: denied")]
    public async Task T15_DelegationChainTooDeep_Denied()
    {
        var (m, ctx) = Default();
        m.SetupActiveDelegation(chainDepth: 2); // tenant max is 1
        m.DelegatorHoldsPermission();

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.DelegationChainTooDeep);
    }

    [Fact(DisplayName = "T16 – valid delegation: chain recorded, evaluation continues as delegator")]
    public async Task T16_ValidDelegation_ChainRecordedAndGranted()
    {
        var (m, ctx) = Default();
        m.SetupActiveDelegation();
        m.DelegatorHoldsPermission();

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
        result.DelegationChain.Should().NotBeNull();
        result.DelegationChain!.DelegatorId.Should().Be(TestIds.DelegatorId);
        result.DelegationChain.DelegateeId.Should().Be(TestIds.UserId);
        result.DelegationChain.ChainDepth.Should().Be(1);
    }

    [Fact(DisplayName = "T17 – no active delegation: delegation step passes through")]
    public async Task T17_NoDelegation_StepPassesThrough()
    {
        var (m, ctx) = Default();
        m.GrantDefaultPermission();
        // DelegSvc returns null (default) → step 3 returns null

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
        result.DelegationChain.Should().BeNull();
    }

    // =========================================================================
    //  STEP 4 – SCOPE INHERITANCE (Tests 18–21)
    // =========================================================================

    [Fact(DisplayName = "T18 – permission on direct scope: granted")]
    public async Task T18_DirectScopePermission_Granted()
    {
        var (m, ctx) = Default();
        m.GrantDefaultPermission(); // granted on TestIds.ScopeId

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
    }

    [Fact(DisplayName = "T19 – permission only on parent scope: inherited and granted")]
    public async Task T19_ParentScopeInheritance_Granted()
    {
        var (m, ctx) = Default();
        m.SetAncestorScopes(TestIds.ParentScopeId);
        m.GrantPermissionAtScope(TestIds.ParentScopeId);

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
    }

    [Fact(DisplayName = "T20 – permission on grandparent scope: multi-level inheritance granted")]
    public async Task T20_GrandparentScopeInheritance_Granted()
    {
        var (m, ctx) = Default();
        m.SetAncestorScopes(TestIds.ParentScopeId, TestIds.GrandparentScopeId);
        m.GrantPermissionAtScope(TestIds.GrandparentScopeId);

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
    }

    [Fact(DisplayName = "T21 – permission at tenant-wide (null) scope: granted")]
    public async Task T21_TenantWideNullScope_Granted()
    {
        var (m, ctx) = Default();
        m.GrantPermissionAtScope(Guid.Empty); // null == tenant-wide
        // Override to null scope
        var dto  = new PermissionDto(TestIds.PermissionId, TestIds.TenantId, TestIds.Action, "r", TestIds.Action, null);
        var list = (IReadOnlyList<PermissionDto>)new List<PermissionDto> { dto };
        m.RbacSvc.GetEffectivePermissionsAsync(
                TestIds.UserId, TestIds.TenantId, (Guid?)null, Arg.Any<CancellationToken>())
             .Returns(list);

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
    }

    // =========================================================================
    //  STEP 5 – ABAC POLICY EVALUATION (Tests 22–23)
    // =========================================================================

    [Fact(DisplayName = "T22 – ABAC policy denies: short-circuits even if RBAC would grant")]
    public async Task T22_AbacDeny_ShortCircuitsRbacGrant()
    {
        var (m, ctx) = Default();
        m.GrantDefaultPermission(); // RBAC would grant
        m.SetAbacDeny("business-hours-deny");

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.AbacConditionFailed);
        result.MatchedPolicyId.Should().Be("business-hours-deny");
    }

    [Fact(DisplayName = "T23 – ABAC policy allows: pipeline continues to RBAC")]
    public async Task T23_AbacAllow_PipelineContinuesToRbac()
    {
        var (m, ctx) = Default();
        m.SetAbacAllow();
        m.GrantDefaultPermission();

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
    }

    // =========================================================================
    //  STEP 6 – RBAC PERMISSION CHECK (Tests 24–25)
    // =========================================================================

    [Fact(DisplayName = "T24 – user holds RBAC permission: granted")]
    public async Task T24_UserHoldsPermission_Granted()
    {
        var (m, ctx) = Default();
        m.GrantDefaultPermission();

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeTrue();
        result.CacheHit.Should().BeFalse();
    }

    [Fact(DisplayName = "T25 – user lacks RBAC permission: falls to default deny")]
    public async Task T25_UserLacksPermission_DefaultDeny()
    {
        var (m, ctx) = Default();
        // No permissions configured → empty list (default)

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.NoPermissionFound);
    }

    // =========================================================================
    //  STEP 7 – DEFAULT DENY (Test 26)
    // =========================================================================

    [Fact(DisplayName = "T26 – no step granted access: default deny fires")]
    public async Task T26_DefaultDeny_NoStepGranted()
    {
        var (m, ctx) = Default();

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.UnknownAction, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.NoPermissionFound);
        result.DiagnosticMessage.Should().Contain(TestIds.UnknownAction);
    }

    // =========================================================================
    //  AUDIT LOGGER (Tests 27–28)
    // =========================================================================

    [Fact(DisplayName = "T27 – audit logger called on granted result")]
    public async Task T27_AuditLogger_CalledOnGrant()
    {
        var (m, ctx) = Default();
        m.GrantDefaultPermission();

        await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        await m.AuditLogger.Received(1).RecordAccessDecisionAsync(
            Arg.Is<AccessDecisionEntry>(e => e.IsGranted && e.UserId == TestIds.UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "T28 – audit logger called on denied result")]
    public async Task T28_AuditLogger_CalledOnDeny()
    {
        var (m, ctx) = Default();
        // No permissions → denied

        await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        await m.AuditLogger.Received(1).RecordAccessDecisionAsync(
            Arg.Is<AccessDecisionEntry>(e => !e.IsGranted && e.UserId == TestIds.UserId),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    //  CONFLICT & PRECEDENCE (Tests 29–30)
    // =========================================================================

    [Fact(DisplayName = "T29 – global deny beats RBAC grant (deny-overrides-allow)")]
    public async Task T29_GlobalDenyBeatRbacGrant()
    {
        var (m, ctx) = Default();
        m.GrantDefaultPermission();   // RBAC would grant
        m.SetGlobalDeny();            // global deny takes priority

        var result = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.ExplicitGlobalDeny);
    }

    [Fact(DisplayName = "T30 – evaluation latency is non-negative on every path")]
    public async Task T30_EvaluationLatency_AlwaysNonNegative()
    {
        var (m, ctx) = Default();

        var r1 = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        m.SetCacheHit(AccessResult.GrantedFromCache());
        var r2 = await m.BuildService().CanUserAccessAsync(
            TestIds.UserId, TestIds.Action, TestIds.ResourceId, TestIds.ScopeId, ctx);

        r1.EvaluationLatencyMs.Should().BeGreaterThanOrEqualTo(0);
        r2.EvaluationLatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }
}
