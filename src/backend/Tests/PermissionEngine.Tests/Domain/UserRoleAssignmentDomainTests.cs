// =============================================================================
//  UserRoleAssignmentDomainTests.cs  –  12 cases
// =============================================================================

using BuildingBlocks.Domain;
using FluentAssertions;
using RbacCore.Domain.Entities;
using BuildingBlocks.Domain.Events;
using Xunit;

namespace PermissionEngine.Tests.Domain;

public sealed class UserRoleAssignmentDomainTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User   = Guid.NewGuid();
    private static readonly Guid Role   = Guid.NewGuid();
    private static readonly Guid Admin  = Guid.NewGuid();
    private static readonly Guid Scope  = Guid.NewGuid();

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "A01 – valid create sets IsActive=true and emits event")]
    public void A01_Create_Valid_IsActiveAndEmitsEvent()
    {
        var a = UserRoleAssignment.Create(Tenant, User, Role, Scope, null, Admin);

        a.IsActive.Should().BeTrue();
        a.IsExpired().Should().BeFalse();
        a.IsEffective().Should().BeTrue();
        a.DomainEvents.Should().ContainSingle(e => e is UserRoleAssignedEvent);
    }

    [Fact(DisplayName = "A02 – empty TenantId throws INVALID_TENANT_ID")]
    public void A02_Create_EmptyTenantId_Throws()
        => Assert.Throws<DomainException>(() =>
                UserRoleAssignment.Create(Guid.Empty, User, Role, null, null, Admin))
              .Code.Should().Be("INVALID_TENANT_ID");

    [Fact(DisplayName = "A03 – empty UserId throws INVALID_USER_ID")]
    public void A03_Create_EmptyUserId_Throws()
        => Assert.Throws<DomainException>(() =>
                UserRoleAssignment.Create(Tenant, Guid.Empty, Role, null, null, Admin))
              .Code.Should().Be("INVALID_USER_ID");

    [Fact(DisplayName = "A04 – empty RoleId throws INVALID_ROLE_ID")]
    public void A04_Create_EmptyRoleId_Throws()
        => Assert.Throws<DomainException>(() =>
                UserRoleAssignment.Create(Tenant, User, Guid.Empty, null, null, Admin))
              .Code.Should().Be("INVALID_ROLE_ID");

    [Fact(DisplayName = "A05 – ExpiresAt in past throws INVALID_EXPIRY")]
    public void A05_Create_PastExpiry_Throws()
        => Assert.Throws<DomainException>(() =>
                UserRoleAssignment.Create(Tenant, User, Role, null,
                    DateTimeOffset.UtcNow.AddSeconds(-1), Admin))
              .Code.Should().Be("INVALID_EXPIRY");

    [Fact(DisplayName = "A06 – future ExpiresAt creates without error")]
    public void A06_Create_FutureExpiry_Succeeds()
    {
        var a = UserRoleAssignment.Create(Tenant, User, Role, null,
            DateTimeOffset.UtcNow.AddHours(1), Admin);
        a.IsExpired().Should().BeFalse();
        a.IsEffective().Should().BeTrue();
    }

    [Fact(DisplayName = "A07 – null ScopeId (tenant-wide) is allowed")]
    public void A07_Create_NullScope_Allowed()
    {
        var a = UserRoleAssignment.Create(Tenant, User, Role, null, null, Admin);
        a.ScopeId.Should().BeNull();
    }

    // ── Deactivate ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "A08 – deactivate active assignment sets IsActive=false and emits event")]
    public void A08_Deactivate_Active_SetsInactiveEmitsEvent()
    {
        var a = UserRoleAssignment.Create(Tenant, User, Role, null, null, Admin);
        a.ClearDomainEvents();
        a.Deactivate("ManualRevocation", Admin);

        a.IsActive.Should().BeFalse();
        a.IsEffective().Should().BeFalse();
        a.DeactivatedReason.Should().Be("ManualRevocation");
        a.DeactivatedAt.Should().NotBeNull();
        a.DomainEvents.Should().ContainSingle(e => e is UserRoleRevokedEvent);
    }

    [Fact(DisplayName = "A09 – deactivate already-inactive is idempotent (no event)")]
    public void A09_Deactivate_AlreadyInactive_Idempotent()
    {
        var a = UserRoleAssignment.Create(Tenant, User, Role, null, null, Admin);
        a.Deactivate("first", Admin);
        a.ClearDomainEvents();
        a.Deactivate("second", Admin);

        a.DomainEvents.Should().BeEmpty();
        a.DeactivatedReason.Should().Be("first"); // original reason preserved
    }

    // ── IsExpired / IsEffective ───────────────────────────────────────────────

    [Fact(DisplayName = "A10 – IsExpired returns false before ExpiresAt")]
    public void A10_IsExpired_BeforeExpiry_ReturnsFalse()
    {
        var a = UserRoleAssignment.Create(Tenant, User, Role, null,
            DateTimeOffset.UtcNow.AddHours(2), Admin);
        a.IsExpired().Should().BeFalse();
    }

    [Fact(DisplayName = "A11 – IsEffective returns false when deactivated even if not expired")]
    public void A11_IsEffective_Deactivated_ReturnsFalse()
    {
        var a = UserRoleAssignment.Create(Tenant, User, Role, null,
            DateTimeOffset.UtcNow.AddHours(2), Admin);
        a.Deactivate("test", Admin);

        a.IsEffective().Should().BeFalse();
    }

    [Fact(DisplayName = "A12 – no ExpiresAt → IsExpired always false")]
    public void A12_NoExpiry_IsExpiredAlwaysFalse()
    {
        var a = UserRoleAssignment.Create(Tenant, User, Role, null, null, Admin);
        a.IsExpired().Should().BeFalse();
    }
}
