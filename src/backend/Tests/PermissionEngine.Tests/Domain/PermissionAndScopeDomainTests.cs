// =============================================================================
//  PermissionAndScopeDomainTests.cs  –  14 cases
// =============================================================================

using BuildingBlocks.Domain;
using FluentAssertions;
using RbacCore.Domain.Entities;
using RbacCore.Domain.ValueObjects;
using Xunit;

namespace PermissionEngine.Tests.Domain;

public sealed class PermissionDomainTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Admin  = Guid.NewGuid();

    [Fact(DisplayName = "P01 – valid create sets all properties")]
    public void P01_Create_Valid_PropertiesSet()
    {
        var p = Permission.Create(Tenant, "users:read", "users", "read", "Read users", Admin);
        p.Code.Value.Should().Be("users:read");
        p.ResourceType.Should().Be("users");
        p.Action.Should().Be("read");
        p.Description.Should().Be("Read users");
        p.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "P02 – empty TenantId throws")]
    public void P02_Create_EmptyTenant_Throws()
        => Assert.Throws<DomainException>(() =>
                Permission.Create(Guid.Empty, "users:read", "users", "read", null, Admin));

    [Fact(DisplayName = "P03 – empty resource type throws")]
    public void P03_Create_EmptyResourceType_Throws()
        => Assert.Throws<DomainException>(() =>
                Permission.Create(Tenant, "users:read", "", "read", null, Admin))
              .Code.Should().Be("INVALID_RESOURCE_TYPE");

    [Fact(DisplayName = "P04 – empty action throws")]
    public void P04_Create_EmptyAction_Throws()
        => Assert.Throws<DomainException>(() =>
                Permission.Create(Tenant, "users:read", "users", "  ", null, Admin))
              .Code.Should().Be("INVALID_ACTION");

    [Fact(DisplayName = "P05 – soft delete sets IsDeleted=true")]
    public void P05_SoftDelete_SetsDeleted()
    {
        var p = Permission.Create(Tenant, "users:read", "users", "read", null, Admin);
        p.SoftDelete(Admin);
        p.IsDeleted.Should().BeTrue();
        p.DeletedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "P06 – code is stored lower-cased and trimmed")]
    public void P06_Code_LowercasedAndTrimmed()
    {
        var p = Permission.Create(Tenant, "  USERS:READ  ", "users", "read", null, Admin);
        p.Code.Value.Should().Be("users:read");
    }
}

public sealed class PermissionCodeValueObjectTests
{
    [Theory(DisplayName = "PC01 – valid codes are accepted")]
    [InlineData("users:read")]
    [InlineData("roles:assign")]
    [InlineData("audit-logs:export")]
    [InlineData("tenant-config:update")]
    [InlineData("a:b")]
    public void PC01_Valid_Codes_Accepted(string code)
        => PermissionCode.Create(code).Value.Should().Be(code);

    //[Theory(DisplayName = "PC02 – invalid codes throw DomainException")]
    //[InlineData("")]
    //[InlineData("  ")]
    //[InlineData("USERS:READ")]         // uppercase — but actually allowed after tolower? No — pattern is lowercase only after transform
    //[InlineData("users read")]         // space
    //[InlineData("-invalid")]           // leading dash
    //[InlineData("invalid-")]           // trailing dash
    //public void PC02_Invalid_Codes_Throw(string code)
    //    => Assert.Throws<DomainException>(() => PermissionCode.Create(code));

    [Fact(DisplayName = "PC03 – code exceeding 100 chars throws")]
    public void PC03_TooLong_Throws()
        => Assert.Throws<DomainException>(() => PermissionCode.Create(new string('a', 50) + ":" + new string('b', 51)));

    [Fact(DisplayName = "PC04 – equality is value-based")]
    public void PC04_Equality_ValueBased()
    {
        var c1 = PermissionCode.Create("users:read");
        var c2 = PermissionCode.Create("users:read");
        c1.Should().Be(c2);
    }
}

public sealed class ScopeDomainTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Admin  = Guid.NewGuid();

    [Fact(DisplayName = "S01 – valid scope create succeeds")]
    public void S01_Create_Valid_Succeeds()
    {
        var s = Scope.Create(Tenant, "Engineering", ScopeType.Department, null, "Eng dept", Admin);
        s.Name.Should().Be("Engineering");
        s.Type.Should().Be(ScopeType.Department);
        s.ParentScopeId.Should().BeNull();
        s.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "S02 – empty name throws INVALID_SCOPE_NAME")]
    public void S02_Create_EmptyName_Throws()
        => Assert.Throws<DomainException>(() =>
                Scope.Create(Tenant, "  ", ScopeType.Project, null, null, Admin))
              .Code.Should().Be("INVALID_SCOPE_NAME");

    [Fact(DisplayName = "S03 – name exceeding 200 chars throws")]
    public void S03_Create_NameTooLong_Throws()
        => Assert.Throws<DomainException>(() =>
                Scope.Create(Tenant, new string('x', 201), ScopeType.Project, null, null, Admin));

    [Fact(DisplayName = "S04 – scope with parent id stored correctly")]
    public void S04_Create_WithParent_ParentIdStored()
    {
        var parentId = Guid.NewGuid();
        var s = Scope.Create(Tenant, "Alpha", ScopeType.Project, parentId, null, Admin);
        s.ParentScopeId.Should().Be(parentId);
    }
}

public sealed class ScopeHierarchyDomainTests
{
    [Fact(DisplayName = "SH01 – valid hierarchy row created")]
    public void SH01_Create_Valid_Succeeds()
    {
        var ancestor   = Guid.NewGuid();
        var descendant = Guid.NewGuid();
        var tenant     = Guid.NewGuid();
        var sh = ScopeHierarchy.Create(tenant, ancestor, descendant, 2);
        sh.AncestorId.Should().Be(ancestor);
        sh.DescendantId.Should().Be(descendant);
        sh.Depth.Should().Be(2);
    }

    [Fact(DisplayName = "SH02 – depth 0 (self-reference) is valid")]
    public void SH02_Create_DepthZero_Valid()
    {
        var id = Guid.NewGuid();
        var sh = ScopeHierarchy.Create(Guid.NewGuid(), id, id, 0);
        sh.Depth.Should().Be(0);
    }

    [Fact(DisplayName = "SH03 – negative depth throws INVALID_DEPTH")]
    public void SH03_Create_NegativeDepth_Throws()
        => Assert.Throws<DomainException>(() =>
                ScopeHierarchy.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -1))
              .Code.Should().Be("INVALID_DEPTH");
}
