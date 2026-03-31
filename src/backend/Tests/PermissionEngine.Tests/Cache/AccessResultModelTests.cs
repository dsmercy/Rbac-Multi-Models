using FluentAssertions;
using PermissionEngine.Domain.Models;
using Xunit;

namespace PermissionEngine.Tests.Cache;

public sealed class AccessResultModelTests
{
    [Fact]
    public void Granted_SetsIsGrantedTrue()
    {
        var result = AccessResult.Granted(cacheHit: false, latencyMs: 12);

        result.IsGranted.Should().BeTrue();
        result.Reason.Should().BeNull();
        result.CacheHit.Should().BeFalse();
        result.EvaluationLatencyMs.Should().Be(12);
    }

    [Fact]
    public void Granted_WithCacheHit_SetsCacheHitTrue()
    {
        var result = AccessResult.Granted(cacheHit: true, latencyMs: 1);

        result.IsGranted.Should().BeTrue();
        result.CacheHit.Should().BeTrue();
        result.EvaluationLatencyMs.Should().Be(1);
    }

    [Fact]
    public void Granted_WithDelegationChain_StoresDelegationInfo()
    {
        var chain = new DelegationChainInfo(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ChainDepth: 1);

        var result = AccessResult.Granted(cacheHit: false, latencyMs: 20,
            delegationChain: chain);

        result.IsGranted.Should().BeTrue();
        result.DelegationChain.Should().Be(chain);
    }

    [Fact]
    public void Denied_SetsIsGrantedFalse_WithReason()
    {
        var result = AccessResult.Denied(
            DenialReason.NoPermissionFound, latencyMs: 15);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.NoPermissionFound);
        result.CacheHit.Should().BeFalse();
        result.EvaluationLatencyMs.Should().Be(15);
    }

    [Fact]
    public void Denied_WithMatchedPolicyId_StoresPolicyId()
    {
        var result = AccessResult.Denied(
            DenialReason.AbacConditionFailed, latencyMs: 30,
            matchedPolicyId: "policy-abc");

        result.IsGranted.Should().BeFalse();
        result.MatchedPolicyId.Should().Be("policy-abc");
    }

    [Fact]
    public void DeniedFromCache_SetsCacheHitTrue_LatencyZero()
    {
        var result = AccessResult.DeniedFromCache(DenialReason.TenantSuspended);

        result.IsGranted.Should().BeFalse();
        result.Reason.Should().Be(DenialReason.TenantSuspended);
        result.CacheHit.Should().BeTrue();
        result.EvaluationLatencyMs.Should().Be(0);
    }

    [Fact]
    public void Granted_NoDelegationChain_IsNull()
    {
        var result = AccessResult.Granted(cacheHit: false, latencyMs: 5);

        result.DelegationChain.Should().BeNull();
        result.MatchedPolicyId.Should().BeNull();
    }
}