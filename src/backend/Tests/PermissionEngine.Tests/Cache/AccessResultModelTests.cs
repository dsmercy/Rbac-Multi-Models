// =============================================================================
//  AccessResultModelTests.cs  –  AccessResult & EvaluationContext (14 cases)
// =============================================================================

using FluentAssertions;
using PermissionEngine.Domain.Models;
using Xunit;

namespace PermissionEngine.Tests.Cache;

public sealed class AccessResultModelTests
{
    // ── Granted factory ───────────────────────────────────────────────────────

    [Fact(DisplayName = "AR01 – Granted: IsGranted=true, Reason=null, CacheHit=false")]
    public void AR01_Granted_PropertiesCorrect()
    {
        var r = AccessResult.Granted(cacheHit: false, latencyMs: 42);
        r.IsGranted.Should().BeTrue();
        r.Reason.Should().BeNull();
        r.CacheHit.Should().BeFalse();
        r.EvaluationLatencyMs.Should().Be(42);
    }

    [Fact(DisplayName = "AR02 – Granted with delegation chain: chain recorded")]
    public void AR02_Granted_WithDelegation_ChainRecorded()
    {
        var chain = new DelegationChainInfo(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        var r = AccessResult.Granted(false, 10, chain);
        r.DelegationChain.Should().Be(chain);
        r.DelegationChain!.ChainDepth.Should().Be(1);
    }

    [Fact(DisplayName = "AR03 – GrantedFromCache: CacheHit=true, latency=0")]
    public void AR03_GrantedFromCache_Properties()
    {
        var r = AccessResult.GrantedFromCache();
        r.IsGranted.Should().BeTrue();
        r.CacheHit.Should().BeTrue();
        r.EvaluationLatencyMs.Should().Be(0);
    }

    // ── Denied factory ────────────────────────────────────────────────────────

    [Fact(DisplayName = "AR04 – Denied: IsGranted=false, Reason set, CacheHit=false")]
    public void AR04_Denied_PropertiesCorrect()
    {
        var r = AccessResult.Denied(DenialReason.NoPermissionFound, 15, "No perms");
        r.IsGranted.Should().BeFalse();
        r.Reason.Should().Be(DenialReason.NoPermissionFound);
        r.CacheHit.Should().BeFalse();
        r.DiagnosticMessage.Should().Be("No perms");
        r.EvaluationLatencyMs.Should().Be(15);
    }

    [Fact(DisplayName = "AR05 – DeniedFromCache: CacheHit=true, latency=0")]
    public void AR05_DeniedFromCache_Properties()
    {
        var r = AccessResult.DeniedFromCache(DenialReason.TenantSuspended);
        r.IsGranted.Should().BeFalse();
        r.CacheHit.Should().BeTrue();
        r.Reason.Should().Be(DenialReason.TenantSuspended);
        r.EvaluationLatencyMs.Should().Be(0);
    }

    [Fact(DisplayName = "AR06 – Denied with matchedPolicyId: policy id recorded")]
    public void AR06_Denied_WithPolicyId_Recorded()
    {
        var r = AccessResult.Denied(DenialReason.ExplicitGlobalDeny, 5, null, "policy-abc");
        r.MatchedPolicyId.Should().Be("policy-abc");
    }

    // ── EvaluatedPolicies & EffectiveRoles ────────────────────────────────────

    [Fact(DisplayName = "AR07 – default EvaluatedPolicies is empty list")]
    public void AR07_Default_EvaluatedPolicies_Empty()
        => AccessResult.Denied(DenialReason.NoPermissionFound, 0)
                       .EvaluatedPolicies.Should().BeEmpty();

    [Fact(DisplayName = "AR08 – default EffectiveRoles is empty list")]
    public void AR08_Default_EffectiveRoles_Empty()
        => AccessResult.Granted(false, 0)
                       .EffectiveRoles.Should().BeEmpty();

    [Fact(DisplayName = "AR09 – Granted with policies and roles: all stored")]
    public void AR09_Granted_WithPoliciesAndRoles_Stored()
    {
        var policies = new[] { new EvaluatedPolicy("p1", "Allow Hours", PolicyOutcome.Allow) };
        var roles    = new[] { "editors", "viewers" };
        var r = AccessResult.Granted(false, 20, evaluatedPolicies: policies, effectiveRoles: roles);

        r.EvaluatedPolicies.Should().HaveCount(1);
        r.EvaluatedPolicies[0].PolicyName.Should().Be("Allow Hours");
        r.EffectiveRoles.Should().BeEquivalentTo(roles);
    }
}

public sealed class EvaluationContextModelTests
{
    [Fact(DisplayName = "EC01 – default constructor: all attribute bags empty")]
    public void EC01_DefaultCtor_AttributeBagsEmpty()
    {
        var ctx = new EvaluationContext();
        ctx.UserAttributes.Should().BeEmpty();
        ctx.ResourceAttributes.Should().BeEmpty();
        ctx.EnvironmentAttributes.Should().BeEmpty();
        ctx.TokenVersion.Should().BeNull();
        ctx.IsSuperAdmin.Should().BeFalse();
    }

    [Fact(DisplayName = "EC02 – full constructor: all properties set")]
    public void EC02_FullCtor_PropertiesSet()
    {
        var tid = Guid.NewGuid();
        var corr = Guid.NewGuid();
        var ctx = new EvaluationContext(
            tenantId: tid, correlationId: corr,
            tokenVersion: 5, isSuperAdmin: true,
            userAttributes: new Dictionary<string, object> { ["dept"] = "Eng" });

        ctx.TenantId.Should().Be(tid);
        ctx.CorrelationId.Should().Be(corr);
        ctx.TokenVersion.Should().Be(5);
        ctx.IsSuperAdmin.Should().BeTrue();
        ctx.UserAttributes["dept"].Should().Be("Eng");
    }

    [Fact(DisplayName = "EC03 – attribute dictionaries are copies (mutation does not affect ctx)")]
    public void EC03_AttributeDicts_AreCopied()
    {
        var source = new Dictionary<string, object> { ["k"] = "v" };
        var ctx = new EvaluationContext(Guid.NewGuid(), Guid.NewGuid(), userAttributes: source);
        source["k"] = "mutated";
        ctx.UserAttributes["k"].Should().Be("v");
    }

    [Fact(DisplayName = "EC04 – ContextBuilder produces expected context")]
    public void EC04_ContextBuilder_ProducesCorrect()
    {
        var ctx = new ContextBuilder()
            .WithTenant(TestIds.TenantId)
            .WithTokenVersion(7)
            .AsSuperAdmin()
            .WithUserAttr("dept", "Eng")
            .WithEnvAttr("ip", "10.0.0.1")
            .Build();

        ctx.TenantId.Should().Be(TestIds.TenantId);
        ctx.TokenVersion.Should().Be(7);
        ctx.IsSuperAdmin.Should().BeTrue();
        ctx.UserAttributes["dept"].Should().Be("Eng");
        ctx.EnvironmentAttributes["ip"].Should().Be("10.0.0.1");
    }

    [Fact(DisplayName = "EC05 – DelegationChainInfo record equality is value-based")]
    public void EC05_DelegationChainInfo_ValueEquality()
    {
        var id = Guid.NewGuid(); var d = Guid.NewGuid(); var e = Guid.NewGuid();
        var c1 = new DelegationChainInfo(id, d, e, 1);
        var c2 = new DelegationChainInfo(id, d, e, 1);
        c1.Should().Be(c2);
    }
}
