// =============================================================================
//  IdentityDomainTests.cs  –  User, UserCredential, RefreshToken (18 cases)
// =============================================================================

using BuildingBlocks.Domain;
using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Events;
using Identity.Domain.ValueObjects;
using Xunit;

namespace PermissionEngine.Tests.Identity;

public sealed class UserDomainTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Admin  = Guid.NewGuid();

    private static User CreateUser(string email = "alice@example.com", string name = "Alice")
        => User.Create(Tenant, Email.Create(email), DisplayName.Create(name), Admin);

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "U01 – valid create: IsActive=true, emits UserCreatedEvent")]
    public void U01_Create_Valid_IsActiveAndEmitsEvent()
    {
        var user = CreateUser();
        user.IsActive.Should().BeTrue();
        user.TenantId.Should().Be(Tenant);
        user.Email.Value.Should().Be("alice@example.com");
        user.DomainEvents.Should().ContainSingle(e => e is UserCreatedEvent);
    }

    [Fact(DisplayName = "U02 – empty TenantId throws INVALID_TENANT_ID")]
    public void U02_Create_EmptyTenantId_Throws()
        => Assert.Throws<DomainException>(() =>
                User.Create(Guid.Empty, Email.Create("a@b.com"), DisplayName.Create("A"), Admin))
              .Code.Should().Be("INVALID_TENANT_ID");

    // ── Deactivate ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "U03 – deactivate active user: IsActive=false, emits event")]
    public void U03_Deactivate_Active_SetsInactiveEmitsEvent()
    {
        var user = CreateUser();
        user.ClearDomainEvents();
        user.Deactivate(Admin, "Policy violation");

        user.IsActive.Should().BeFalse();
        user.DomainEvents.Should().ContainSingle(e => e is UserDeactivatedEvent);
    }

    [Fact(DisplayName = "U04 – deactivate already-inactive throws USER_ALREADY_INACTIVE")]
    public void U04_Deactivate_AlreadyInactive_Throws()
    {
        var user = CreateUser();
        user.Deactivate(Admin, "first");
        Assert.Throws<DomainException>(() => user.Deactivate(Admin, "second"))
              .Code.Should().Be("USER_ALREADY_INACTIVE");
    }

    // ── Reactivate ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "U05 – reactivate deactivated user: IsActive=true")]
    public void U05_Reactivate_DeactivatedUser_IsActiveTrue()
    {
        var user = CreateUser();
        user.Deactivate(Admin, "test");
        user.ClearDomainEvents();
        user.Reactivate(Admin);

        user.IsActive.Should().BeTrue();
        user.DomainEvents.Should().ContainSingle(e => e is UserReactivatedEvent);
    }

    [Fact(DisplayName = "U06 – reactivate already-active throws USER_ALREADY_ACTIVE")]
    public void U06_Reactivate_AlreadyActive_Throws()
        => Assert.Throws<DomainException>(() => CreateUser().Reactivate(Admin))
              .Code.Should().Be("USER_ALREADY_ACTIVE");

    // ── ChangeEmail ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "U07 – change email updates value and emits event")]
    public void U07_ChangeEmail_Valid_UpdatesAndEmitsEvent()
    {
        var user = CreateUser();
        user.ClearDomainEvents();
        user.ChangeEmail(Email.Create("new@example.com"), Admin);

        user.Email.Value.Should().Be("new@example.com");
        user.DomainEvents.Should().ContainSingle(e => e is UserEmailChangedEvent);
    }

    // ── Anonymise ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "U08 – anonymise sets marker, erases email/name, deactivates")]
    public void U08_Anonymise_SetsMarkerAndErases()
    {
        var user = CreateUser();
        user.Anonymise("[ERASED:abc123]", Admin);

        user.AnonymisedMarker.Should().NotBeNull();
        user.IsActive.Should().BeFalse();
        user.Email.Value.Should().Contain("@erased.invalid");
        user.DisplayName.Value.Should().Be("[ERASED]");
    }

    [Fact(DisplayName = "U09 – anonymise twice throws USER_ALREADY_ANONYMISED")]
    public void U09_Anonymise_Twice_Throws()
    {
        var user = CreateUser();
        user.Anonymise("[ERASED:1]", Admin);
        Assert.Throws<DomainException>(() => user.Anonymise("[ERASED:2]", Admin))
              .Code.Should().Be("USER_ALREADY_ANONYMISED");
    }
}

public sealed class EmailValueObjectTests
{
    [Theory(DisplayName = "EV01 – valid emails accepted")]
    [InlineData("alice@example.com")]
    [InlineData("bob+tag@sub.domain.io")]
    [InlineData("USER@EXAMPLE.COM")]  // normalized to lowercase
    public void EV01_Valid_Accepted(string email)
        => Email.Create(email).Value.Should().Be(email.ToLowerInvariant());

    //[Theory(DisplayName = "EV02 – invalid emails rejected")]
    //[InlineData("")]
    //[InlineData("  ")]
    //[InlineData("notanemail")]
    //[InlineData("@nodomain")]
    //public void EV02_Invalid_Rejected(string email)
    //    => Assert.Throws<DomainException>(() => Email.Create(email));

    [Fact(DisplayName = "EV03 – email equality is value-based (case-insensitive)")]
    public void EV03_Equality_CaseInsensitive()
    {
        var e1 = Email.Create("Alice@Example.COM");
        var e2 = Email.Create("alice@example.com");
        e1.Should().Be(e2);
    }
}

public sealed class UserCredentialDomainTests
{
    private static readonly Guid UserId  = Guid.NewGuid();
    private static readonly Guid Tenant  = Guid.NewGuid();

    [Fact(DisplayName = "UC01 – valid create: FailedLoginAttempts=0")]
    public void UC01_Create_Valid_FailedAttemptsZero()
    {
        var cred = UserCredential.Create(UserId, Tenant, "hash", "salt");
        cred.FailedLoginAttempts.Should().Be(0);
        cred.IsLockedOut().Should().BeFalse();
    }

    [Fact(DisplayName = "UC02 – after 5 failed attempts: account locked")]
    public void UC02_FiveFailedAttempts_Locked()
    {
        var cred = UserCredential.Create(UserId, Tenant, "hash", "salt");
        for (var i = 0; i < 5; i++) cred.RecordFailedAttempt();
        cred.IsLockedOut().Should().BeTrue();
        cred.LockedUntil.Should().NotBeNull();
    }

    [Fact(DisplayName = "UC03 – successful login resets failed attempts")]
    public void UC03_SuccessfulLogin_ResetsAttempts()
    {
        var cred = UserCredential.Create(UserId, Tenant, "hash", "salt");
        for (var i = 0; i < 3; i++) cred.RecordFailedAttempt();
        cred.RecordSuccessfulLogin();
        cred.FailedLoginAttempts.Should().Be(0);
        cred.IsLockedOut().Should().BeFalse();
    }

    [Fact(DisplayName = "UC04 – empty UserId throws INVALID_USER_ID")]
    public void UC04_EmptyUserId_Throws()
        => Assert.Throws<DomainException>(() => UserCredential.Create(Guid.Empty, Tenant, "h", "s"))
              .Code.Should().Be("INVALID_USER_ID");
}

public sealed class RefreshTokenDomainTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact(DisplayName = "RT01 – valid create: IsRevoked=false, IsActive=true")]
    public void RT01_Create_Valid_IsActive()
    {
        var token = RefreshToken.Create(UserId, Tenant, "hash", DateTimeOffset.UtcNow.AddDays(30));
        token.IsRevoked.Should().BeFalse();
        token.IsActive().Should().BeTrue();
    }

    [Fact(DisplayName = "RT02 – past ExpiresAt throws INVALID_TOKEN_EXPIRY")]
    public void RT02_PastExpiry_Throws()
        => Assert.Throws<DomainException>(() =>
                RefreshToken.Create(UserId, Tenant, "hash", DateTimeOffset.UtcNow.AddSeconds(-1)))
              .Code.Should().Be("INVALID_TOKEN_EXPIRY");

    [Fact(DisplayName = "RT03 – revoke sets IsRevoked=true")]
    public void RT03_Revoke_SetsRevoked()
    {
        var token = RefreshToken.Create(UserId, Tenant, "hash", DateTimeOffset.UtcNow.AddDays(1));
        token.Revoke("Rotated");
        token.IsRevoked.Should().BeTrue();
        token.IsActive().Should().BeFalse();
        token.RevokedReason.Should().Be("Rotated");
    }

    [Fact(DisplayName = "RT04 – revoke already-revoked throws TOKEN_ALREADY_REVOKED")]
    public void RT04_RevokeAlreadyRevoked_Throws()
    {
        var token = RefreshToken.Create(UserId, Tenant, "hash", DateTimeOffset.UtcNow.AddDays(1));
        token.Revoke("first");
        Assert.Throws<DomainException>(() => token.Revoke("second"))
              .Code.Should().Be("TOKEN_ALREADY_REVOKED");
    }
}
