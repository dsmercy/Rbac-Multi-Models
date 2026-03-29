// =============================================================================
//  DelegationDomainTests.cs  –  14 cases
// =============================================================================

using BuildingBlocks.Domain;
using Delegation.Domain.Entities;
using Delegation.Domain.Events;
using FluentAssertions;
using Xunit;

namespace PermissionEngine.Tests.Delegation;

public sealed class DelegationGrantDomainTests
{
    private static readonly Guid Tenant    = Guid.NewGuid();
    private static readonly Guid Delegator = Guid.NewGuid();
    private static readonly Guid Delegatee = Guid.NewGuid();
    private static readonly Guid Scope     = Guid.NewGuid();
    private static readonly Guid Admin     = Guid.NewGuid();

    private static DelegationGrant Valid(
        IReadOnlyList<string>? codes = null,
        int chainDepth = 1,
        DateTimeOffset? expires = null)
        => DelegationGrant.Create(Tenant, Delegator, Delegatee,
            codes ?? new[] { "users:read" }, Scope,
            expires ?? DateTimeOffset.UtcNow.AddHours(1), chainDepth, Admin);

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "D01 – valid create: IsActive, IsRevoked=false, emits event")]
    public void D01_Create_Valid_StateCorrect()
    {
        var d = Valid();
        d.IsRevoked.Should().BeFalse();
        d.IsActive().Should().BeTrue();
        d.IsExpired().Should().BeFalse();
        d.DomainEvents.Should().ContainSingle(e => e is DelegationCreatedEvent);
    }

    [Fact(DisplayName = "D02 – self-delegation throws SELF_DELEGATION")]
    public void D02_Create_SelfDelegation_Throws()
        => Assert.Throws<DomainException>(() =>
                DelegationGrant.Create(Tenant, Delegator, Delegator,
                    new[] { "users:read" }, Scope, DateTimeOffset.UtcNow.AddHours(1), 1, Admin))
              .Code.Should().Be("SELF_DELEGATION");

    [Fact(DisplayName = "D03 – ExpiresAt in past throws INVALID_EXPIRY")]
    public void D03_Create_PastExpiry_Throws()
        => Assert.Throws<DomainException>(() =>
                DelegationGrant.Create(Tenant, Delegator, Delegatee,
                    new[] { "users:read" }, Scope, DateTimeOffset.UtcNow.AddSeconds(-1), 1, Admin))
              .Code.Should().Be("INVALID_EXPIRY");

    [Fact(DisplayName = "D04 – empty permission codes throw NO_PERMISSIONS")]
    public void D04_Create_NoPermissionCodes_Throws()
        => Assert.Throws<DomainException>(() =>
                DelegationGrant.Create(Tenant, Delegator, Delegatee,
                    Array.Empty<string>(), Scope, DateTimeOffset.UtcNow.AddHours(1), 1, Admin))
              .Code.Should().Be("NO_PERMISSIONS");

    [Fact(DisplayName = "D05 – chain depth 0 throws INVALID_CHAIN_DEPTH")]
    public void D05_Create_ChainDepthZero_Throws()
        => Assert.Throws<DomainException>(() =>
                DelegationGrant.Create(Tenant, Delegator, Delegatee,
                    new[] { "users:read" }, Scope, DateTimeOffset.UtcNow.AddHours(1), 0, Admin))
              .Code.Should().Be("INVALID_CHAIN_DEPTH");

    [Fact(DisplayName = "D06 – empty TenantId throws INVALID_TENANT_ID")]
    public void D06_Create_EmptyTenantId_Throws()
        => Assert.Throws<DomainException>(() =>
                DelegationGrant.Create(Guid.Empty, Delegator, Delegatee,
                    new[] { "users:read" }, Scope, DateTimeOffset.UtcNow.AddHours(1), 1, Admin))
              .Code.Should().Be("INVALID_TENANT_ID");

    [Fact(DisplayName = "D07 – multiple permission codes stored correctly")]
    public void D07_Create_MultiplePermissions_AllStored()
    {
        var codes = new[] { "users:read", "roles:read", "policies:read" };
        var d = Valid(codes: codes);
        d.PermissionCodes.Should().BeEquivalentTo(codes);
    }

    // ── Revoke ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "D08 – revoke active delegation: IsRevoked=true, emits event")]
    public void D08_Revoke_Active_SetsRevokedEmitsEvent()
    {
        var d = Valid();
        d.ClearDomainEvents();
        d.Revoke(Admin);

        d.IsRevoked.Should().BeTrue();
        d.RevokedAt.Should().NotBeNull();
        d.RevokedByUserId.Should().Be(Admin);
        d.IsActive().Should().BeFalse();
        d.DomainEvents.Should().ContainSingle(e => e is DelegationRevokedEvent);
    }

    [Fact(DisplayName = "D09 – revoke already-revoked throws ALREADY_REVOKED")]
    public void D09_Revoke_AlreadyRevoked_Throws()
    {
        var d = Valid();
        d.Revoke(Admin);
        Assert.Throws<DomainException>(() => d.Revoke(Admin))
              .Code.Should().Be("ALREADY_REVOKED");
    }

    // ── IsActive / IsExpired ──────────────────────────────────────────────────

    [Fact(DisplayName = "D10 – active and not expired: IsActive returns true")]
    public void D10_IsActive_NotExpiredNotRevoked_ReturnsTrue()
        => Valid(expires: DateTimeOffset.UtcNow.AddDays(1)).IsActive().Should().BeTrue();

    [Fact(DisplayName = "D11 – revoked: IsActive returns false")]
    public void D11_IsActive_Revoked_ReturnsFalse()
    {
        var d = Valid();
        d.Revoke(Admin);
        d.IsActive().Should().BeFalse();
    }

    [Fact(DisplayName = "D12 – chain depth 1 is stored correctly")]
    public void D12_ChainDepth_StoredCorrectly()
        => Valid(chainDepth: 1).ChainDepth.Should().Be(1);

    [Fact(DisplayName = "D13 – chain depth 3 (max allowed) stores correctly")]
    public void D13_ChainDepth_3_Stored()
        => Valid(chainDepth: 3).ChainDepth.Should().Be(3);

    [Fact(DisplayName = "D14 – DelegationCreatedEvent carries correct payload")]
    public void D14_CreatedEvent_Payload_Correct()
    {
        var d = DelegationGrant.Create(Tenant, Delegator, Delegatee,
            new[] { "users:read" }, Scope, DateTimeOffset.UtcNow.AddHours(1), 1, Admin);

        var evt = d.DomainEvents.OfType<DelegationCreatedEvent>().Single();
        evt.TenantId.Should().Be(Tenant);
        evt.DelegatorId.Should().Be(Delegator);
        evt.DelegateeId.Should().Be(Delegatee);
        evt.ChainDepth.Should().Be(1);
    }
}
