using FluentAssertions;
using Identity.Application.Common;
using Identity.Application.Services;
using Identity.Infrastructure.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace PermissionEngine.Tests.Pipeline;

/// <summary>
/// Unit tests for JwtTokenService Phase 4 claims schema.
///
/// Covers:
///   JWT-01  Standard claims (sub, tid, email, jti) are present
///   JWT-02  Roles claim array is embedded
///   JWT-03  Scope claim (scp) uses "scope:{uuid}" format
///   JWT-04  Token version (tv) claim is embedded
///   JWT-05  is_super_admin claim is embedded correctly
///   JWT-06  Delegation claims (del, del_chain) are present when provided
///   JWT-07  Delegation claims are absent when not a delegatee
///   JWT-08  Access token expiry matches configured TTL
///   JWT-09  TokenVersion is propagated to TokenPair return value
/// </summary>
public sealed class JwtTokenServiceTests
{
    private static readonly JwtSettings Settings = new()
    {
        SigningKey             = "test-super-secret-key-at-least-32-chars-long!",
        Issuer                 = "rbac-test",
        Audience               = "rbac-test-clients",
        AccessTokenExpiryMinutes = 15
    };

    private static JwtTokenService CreateSut()
        => new(Options.Create(Settings));

    private static UserDto SampleUser(Guid? tenantId = null) => new(
        Id:          Guid.NewGuid(),
        TenantId:    tenantId ?? Guid.NewGuid(),
        Email:       "test@example.com",
        DisplayName: "Test User",
        IsActive:    true,
        CreatedAt:   DateTimeOffset.UtcNow,
        LastLoginAt: null);

    /// <summary>
    /// Decodes the JWT payload as raw JSON without any claim-type mapping.
    /// JsonSerializer.Deserialize&lt;JsonElement&gt; returns a self-contained value copy —
    /// no parent JsonDocument to dispose, unlike JsonDocument.Parse().RootElement.
    /// </summary>
    private static JsonElement DecodePayload(string raw)
    {
        var b64 = raw.Split('.')[1].Replace('-', '+').Replace('_', '/');
        b64 += new string('=', (4 - b64.Length % 4) % 4);
        return JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(b64));
    }

    private static string? GetString(JsonElement payload, string key)
        => payload.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static IReadOnlyList<string> GetArray(JsonElement payload, string key)
    {
        if (!payload.TryGetProperty(key, out var prop)) return [];
        if (prop.ValueKind == JsonValueKind.Array)
            return prop.EnumerateArray().Select(e => e.GetString()!).ToList();
        if (prop.ValueKind == JsonValueKind.String)
            return [prop.GetString()!];
        return [];
    }

    // JWT-01 ──────────────────────────────────────────────────────────────────
    [Fact]
    public void GenerateTokenPair_EmitsStandardClaims()
    {
        var user = SampleUser();
        var sut  = CreateSut();

        var pair    = sut.GenerateTokenPair(new TokenGenerationParams(user, [], [], false, 0));
        var payload = DecodePayload(pair.AccessToken);

        GetString(payload, "sub").Should().Be(user.Id.ToString());
        GetString(payload, "tid").Should().Be(user.TenantId.ToString());
        GetString(payload, "email").Should().Be(user.Email);
        GetString(payload, "jti").Should().NotBeNullOrEmpty();
    }

    // JWT-02 ──────────────────────────────────────────────────────────────────
    [Fact]
    public void GenerateTokenPair_EmbedsRoleClaims()
    {
        var user  = SampleUser();
        var roles = new[] { "tenant-admin", "viewer" };
        var sut   = CreateSut();

        var pair    = sut.GenerateTokenPair(new TokenGenerationParams(user, roles, [], false, 3));
        var payload = DecodePayload(pair.AccessToken);

        GetArray(payload, "roles").Should().BeEquivalentTo(roles);
    }

    // JWT-03 ──────────────────────────────────────────────────────────────────
    [Fact]
    public void GenerateTokenPair_EmbedsScopeClaimsWithPrefix()
    {
        var user   = SampleUser();
        var scope1 = Guid.NewGuid();
        var scope2 = Guid.NewGuid();
        var sut    = CreateSut();

        var pair    = sut.GenerateTokenPair(new TokenGenerationParams(user, [], [scope1, scope2], false, 1));
        var payload = DecodePayload(pair.AccessToken);

        GetArray(payload, "scp").Should().Contain($"scope:{scope1}");
        GetArray(payload, "scp").Should().Contain($"scope:{scope2}");
    }

    // JWT-04 ──────────────────────────────────────────────────────────────────
    [Fact]
    public void GenerateTokenPair_EmbedsTokenVersionClaim()
    {
        var user = SampleUser();
        var sut  = CreateSut();

        var pair    = sut.GenerateTokenPair(new TokenGenerationParams(user, [], [], false, TokenVersion: 42));
        var payload = DecodePayload(pair.AccessToken);

        GetString(payload, "tv").Should().Be("42");
    }

    // JWT-05 ──────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(true,  "true")]
    [InlineData(false, "false")]
    public void GenerateTokenPair_EmbedsSuperAdminClaim(bool isSuperAdmin, string expected)
    {
        var user = SampleUser();
        var sut  = CreateSut();

        var pair    = sut.GenerateTokenPair(new TokenGenerationParams(user, [], [], isSuperAdmin, 0));
        var payload = DecodePayload(pair.AccessToken);

        GetString(payload, "is_super_admin").Should().Be(expected);
    }

    // JWT-06 ──────────────────────────────────────────────────────────────────
    [Fact]
    public void GenerateTokenPair_EmbedsDelegationClaimsWhenPresent()
    {
        var user      = SampleUser();
        var delegator = Guid.NewGuid();
        var chain     = new[] { delegator, user.Id };
        var sut       = CreateSut();

        var pair = sut.GenerateTokenPair(new TokenGenerationParams(
            user, [], [], false, 1,
            DelegatorId:     delegator,
            DelegationChain: chain));

        var payload = DecodePayload(pair.AccessToken);

        GetString(payload, "del").Should().Be(delegator.ToString());
        GetArray(payload, "del_chain").Should().BeEquivalentTo(chain.Select(id => id.ToString()));
    }

    // JWT-07 ──────────────────────────────────────────────────────────────────
    [Fact]
    public void GenerateTokenPair_NoDelegationClaims_WhenNotDelegatee()
    {
        var user = SampleUser();
        var sut  = CreateSut();

        var pair    = sut.GenerateTokenPair(new TokenGenerationParams(user, [], [], false, 0));
        var payload = DecodePayload(pair.AccessToken);

        payload.TryGetProperty("del", out _).Should().BeFalse();
        payload.TryGetProperty("del_chain", out _).Should().BeFalse();
    }

    // JWT-08 ──────────────────────────────────────────────────────────────────
    [Fact]
    public void GenerateTokenPair_AccessTokenExpiry_MatchesConfiguredTtl()
    {
        var user   = SampleUser();
        var sut    = CreateSut();
        var before = DateTimeOffset.UtcNow;

        var pair = sut.GenerateTokenPair(new TokenGenerationParams(user, [], [], false, 0));

        var after   = DateTimeOffset.UtcNow;
        var payload = DecodePayload(pair.AccessToken);

        payload.TryGetProperty("exp", out var expProp).Should().BeTrue();
        var exp = DateTimeOffset.FromUnixTimeSeconds(expProp.GetInt64());

        exp.Should().BeCloseTo(
            before.AddMinutes(Settings.AccessTokenExpiryMinutes),
            precision: TimeSpan.FromSeconds(10),
            because: "access token TTL must match configured 15 minutes");

        pair.AccessTokenExpiresAt.Should().BeCloseTo(
            after.AddMinutes(Settings.AccessTokenExpiryMinutes),
            precision: TimeSpan.FromSeconds(10));
    }

    // JWT-09 ─────────────────────────────────────────────────────────────────
    [Fact]
    public void GenerateTokenPair_TokenVersionPropagatedToReturnValue()
    {
        var user = SampleUser();
        var sut  = CreateSut();

        var pair = sut.GenerateTokenPair(new TokenGenerationParams(
            user, [], [], false, TokenVersion: 99));

        pair.TokenVersion.Should().Be(99);
    }
}
