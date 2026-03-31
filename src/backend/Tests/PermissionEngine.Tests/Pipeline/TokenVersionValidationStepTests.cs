using FluentAssertions;
using NSubstitute;
using PermissionEngine.Application.Pipeline;
using PermissionEngine.Domain.Exceptions;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Domain.Models;
using RbacSystem.Api.Middleware;
using System.Diagnostics;
using Xunit;

namespace PermissionEngine.Tests.Pipeline;

/// <summary>
/// Unit tests for TokenVersionValidationStep (pipeline step 0).
///
/// Covers:
///   TV-01  Valid token version → returns null (continue pipeline)
///   TV-02  Stale token version → throws StaleTokenException (→ HTTP 401)
///   TV-03  Null token version (server-to-server) → skip validation, continue
///   TV-04  Version 0 (new user, no Redis key) → valid against JWT tv=0
///   TV-05  Version 0 JWT vs Redis version 1 → stale, throw
///   TV-06  High version numbers → correct comparison
/// </summary>
public sealed class TokenVersionValidationStepTests
{
    private readonly IPermissionCacheService _cache =
        Substitute.For<IPermissionCacheService>();

    private TokenVersionValidationStep Sut => new(_cache);

    private static EvaluationRequest MakeRequest(int? tokenVersion)
        => new()
        {
            UserId = Guid.NewGuid(),
            Action = "users:read",
            ResourceId = Guid.NewGuid(),
            ScopeId = Guid.NewGuid(),
            Context = new EvaluationContext(
                tenantId: Guid.NewGuid(),
                correlationId: Guid.NewGuid(),
                tokenVersion: tokenVersion),
            StartedAt = Stopwatch.GetTimestamp()
        };

    // TV-01 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ValidTokenVersion_ReturnNull_ContinuesPipeline()
    {
        var request = MakeRequest(tokenVersion: 5);
        _cache.GetTokenVersionAsync(request.UserId, default).Returns(5);

        var result = await Sut.EvaluateAsync(request, default);

        result.Should().BeNull("null means continue to the next pipeline step");
    }

    // TV-02 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task StaleTokenVersion_ThrowsStaleTokenException()
    {
        var request = MakeRequest(tokenVersion: 3);
        _cache.GetTokenVersionAsync(request.UserId, default).Returns(7);

        var act = async () => await Sut.EvaluateAsync(request, default);

        await act.Should().ThrowAsync<StaleTokenException>()
            .WithMessage("*stale*");
    }

    // TV-03 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task NullTokenVersion_SkipsValidation_ReturnNull()
    {
        var request = MakeRequest(tokenVersion: null); // server-to-server

        var result = await Sut.EvaluateAsync(request, default);

        result.Should().BeNull();
        await _cache.DidNotReceive().GetTokenVersionAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // TV-04 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task NewUser_VersionZeroInJwt_MatchesRedisZero_Passes()
    {
        var request = MakeRequest(tokenVersion: 0);
        _cache.GetTokenVersionAsync(request.UserId, default).Returns(0);

        var result = await Sut.EvaluateAsync(request, default);

        result.Should().BeNull();
    }

    // TV-05 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task JwtVersionZero_RedisVersionOne_IsStale()
    {
        var request = MakeRequest(tokenVersion: 0);
        _cache.GetTokenVersionAsync(request.UserId, default).Returns(1);

        var act = async () => await Sut.EvaluateAsync(request, default);

        await act.Should().ThrowAsync<StaleTokenException>();
    }

    // TV-06 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task HighVersionNumbers_ComparedCorrectly()
    {
        var request = MakeRequest(tokenVersion: 999);
        _cache.GetTokenVersionAsync(request.UserId, default).Returns(999);

        var result = await Sut.EvaluateAsync(request, default);

        result.Should().BeNull("version 999 matches Redis version 999");
    }

    // TV-07 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task StaleTokenException_ContainsBothVersionNumbers()
    {
        var request = MakeRequest(tokenVersion: 2);
        _cache.GetTokenVersionAsync(request.UserId, default).Returns(10);

        var act = async () => await Sut.EvaluateAsync(request, default);

        await act.Should().ThrowAsync<StaleTokenException>()
            .WithMessage("*2*")
            .WithMessage("*10*");
    }
}
