// =============================================================================
//  RoleDomainTests.cs  –  Role aggregate invariants (18 cases)
// =============================================================================

using BuildingBlocks.Domain;
using FluentAssertions;
using RbacCore.Domain.Entities;
using BuildingBlocks.Domain.Events;
using Xunit;

namespace PermissionEngine.Tests.Domain;

public sealed class RoleDomainTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Admin  = Guid.NewGuid();
    private static readonly Guid Perm1  = Guid.NewGuid();
    private static readonly Guid Perm2  = Guid.NewGuid();

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "R01 – valid create sets all properties correctly")]
    public void R01_Create_Valid_PropertiesSet()
    {
        var role = Role.Create(Tenant, "editors", "Edit content", Admin);

        role.Name.Should().Be("editors");
        role.TenantId.Should().Be(Tenant);
        role.Description.Should().Be("Edit content");
        role.IsSystemRole.Should().BeFalse();
        role.IsDeleted.Should().BeFalse();
        role.Permissions.Should().BeEmpty();
    }

    [Fact(DisplayName = "R02 – create emits RoleCreatedEvent")]
    public void R02_Create_EmitsRoleCreatedEvent()
    {
        var role = Role.Create(Tenant, "editors", null, Admin);
        role.DomainEvents.Should().ContainSingle(e => e is RoleCreatedEvent);
        var evt = (RoleCreatedEvent)role.DomainEvents[0];
        evt.RoleName.Should().Be("editors");
        evt.TenantId.Should().Be(Tenant);
    }

    [Fact(DisplayName = "R03 – empty name throws DomainException")]
    public void R03_Create_EmptyName_Throws()
        => Assert.Throws<DomainException>(() => Role.Create(Tenant, "  ", null, Admin))
                  .Code.Should().Be("INVALID_ROLE_NAME");

    [Fact(DisplayName = "R04 – empty TenantId throws DomainException")]
    public void R04_Create_EmptyTenantId_Throws()
        => Assert.Throws<DomainException>(() => Role.Create(Guid.Empty, "editors", null, Admin))
                  .Code.Should().Be("INVALID_TENANT_ID");

    [Fact(DisplayName = "R05 – name exceeding 100 chars throws DomainException")]
    public void R05_Create_NameTooLong_Throws()
        => Assert.Throws<DomainException>(() => Role.Create(Tenant, new string('x', 101), null, Admin));

    [Fact(DisplayName = "R06 – system role flag preserved")]
    public void R06_Create_SystemRole_FlagPreserved()
    {
        var role = Role.Create(Tenant, "sys-admin", null, Admin, isSystemRole: true);
        role.IsSystemRole.Should().BeTrue();
    }

    // ── AddPermission ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "R07 – add new permission succeeds and emits event")]
    public void R07_AddPermission_New_AddsAndEmitsEvent()
    {
        var role = Role.Create(Tenant, "editors", null, Admin);
        role.ClearDomainEvents();
        role.AddPermission(Perm1, Admin);

        role.Permissions.Should().ContainSingle(rp => rp.PermissionId == Perm1);
        role.DomainEvents.Should().ContainSingle(e => e is PermissionGrantedEvent);
    }

    [Fact(DisplayName = "R08 – add same permission twice is idempotent")]
    public void R08_AddPermission_Duplicate_Idempotent()
    {
        var role = Role.Create(Tenant, "editors", null, Admin);
        role.AddPermission(Perm1, Admin);
        role.ClearDomainEvents();
        role.AddPermission(Perm1, Admin); // second add

        role.Permissions.Should().HaveCount(1);
        role.DomainEvents.Should().BeEmpty(); // no second event
    }

    [Fact(DisplayName = "R09 – add permission to deleted role throws")]
    public void R09_AddPermission_DeletedRole_Throws()
    {
        var role = Role.Create(Tenant, "editors", null, Admin);
        role.SoftDelete(Admin);

        Assert.Throws<DomainException>(() => role.AddPermission(Perm1, Admin))
              .Code.Should().Be("ROLE_DELETED");
    }

    // ── RemovePermission ──────────────────────────────────────────────────────

    [Fact(DisplayName = "R10 – remove existing permission succeeds and emits event")]
    public void R10_RemovePermission_Existing_RemovesAndEmitsEvent()
    {
        var role = Role.Create(Tenant, "editors", null, Admin);
        role.AddPermission(Perm1, Admin);
        role.ClearDomainEvents();

        role.RemovePermission(Perm1, Admin);

        role.Permissions.Should().BeEmpty();
        role.DomainEvents.Should().ContainSingle(e => e is PermissionRevokedEvent);
    }

    [Fact(DisplayName = "R11 – remove non-existent permission is idempotent")]
    public void R11_RemovePermission_NotPresent_Idempotent()
    {
        var role = Role.Create(Tenant, "editors", null, Admin);
        role.ClearDomainEvents();

        role.RemovePermission(Perm1, Admin); // never existed

        role.DomainEvents.Should().BeEmpty();
    }

    [Fact(DisplayName = "R12 – remove one of two permissions leaves the other")]
    public void R12_RemovePermission_OneOfTwo_OtherRemains()
    {
        var role = Role.Create(Tenant, "editors", null, Admin);
        role.AddPermission(Perm1, Admin);
        role.AddPermission(Perm2, Admin);
        role.ClearDomainEvents();

        role.RemovePermission(Perm1, Admin);

        role.Permissions.Should().ContainSingle(rp => rp.PermissionId == Perm2);
    }

    // ── Rename ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "R13 – rename system role throws SYSTEM_ROLE_IMMUTABLE")]
    public void R13_Rename_SystemRole_Throws()
    {
        var role = Role.Create(Tenant, "sys-admin", null, Admin, isSystemRole: true);
        Assert.Throws<DomainException>(() => role.Rename("new-name", null, Admin))
              .Code.Should().Be("SYSTEM_ROLE_IMMUTABLE");
    }

    [Fact(DisplayName = "R14 – rename user role updates name")]
    public void R14_Rename_UserRole_UpdatesName()
    {
        var role = Role.Create(Tenant, "editors", null, Admin);
        role.Rename("senior-editors", "Updated desc", Admin);
        role.Name.Should().Be("senior-editors");
        role.Description.Should().Be("Updated desc");
    }

    // ── SoftDelete ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "R15 – soft delete system role throws SYSTEM_ROLE_IMMUTABLE")]
    public void R15_SoftDelete_SystemRole_Throws()
    {
        var role = Role.Create(Tenant, "sys-admin", null, Admin, isSystemRole: true);
        Assert.Throws<DomainException>(() => role.SoftDelete(Admin))
              .Code.Should().Be("SYSTEM_ROLE_IMMUTABLE");
    }

    [Fact(DisplayName = "R16 – soft delete user role sets IsDeleted and emits event")]
    public void R16_SoftDelete_UserRole_SetsDeletedEmitsEvent()
    {
        var role = Role.Create(Tenant, "editors", null, Admin);
        role.ClearDomainEvents();
        role.SoftDelete(Admin);

        role.IsDeleted.Should().BeTrue();
        role.DeletedAt.Should().NotBeNull();
        role.DeletedBy.Should().Be(Admin);
        role.DomainEvents.Should().ContainSingle(e => e is RoleDeletedEvent);
    }

    [Fact(DisplayName = "R17 – cannot add permission after soft delete")]
    public void R17_AddPermission_AfterDelete_Throws()
    {
        var role = Role.Create(Tenant, "editors", null, Admin);
        role.SoftDelete(Admin);

        Assert.Throws<DomainException>(() => role.AddPermission(Perm1, Admin));
    }

    [Fact(DisplayName = "R18 – soft delete twice throws ALREADY_DELETED")]
    public void R18_SoftDelete_Twice_Throws()
    {
        var role = Role.Create(Tenant, "editors", null, Admin);
        role.SoftDelete(Admin);

        Assert.Throws<DomainException>(() => role.SoftDelete(Admin))
              .Code.Should().Be("ALREADY_DELETED");
    }
}
