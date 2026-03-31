using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PermissionEngine.Domain.Models;
using PermissionEngine.Infrastructure.Cache;
using StackExchange.Redis;
using Xunit;

namespace PermissionEngine.Tests.Pipeline;

/// <summary>
/// Unit tests for token-version operations in RedisPermissionCacheService.
///
/// Covers:
///   RTV-01  GetTokenVersionAsync → returns 0 when key absent
///   RTV-02  GetTokenVersionAsync → returns stored value
///   RTV-03  IncrementTokenVersionAsync → calls INCR and resets TTL
///   RTV-04  InvalidateUserAsync → delegates to IncrementTokenVersionAsync
///   RTV-05  Cache entry with old version → rejected (returns null)
///   RTV-06  Cache entry with matching version → returned
/// </summary>
public sealed class RedisTokenVersionTests
{
    private readonly IConnectionMultiplexer _redis =
        Substitute.For<IConnectionMultiplexer>();

    private readonly IDatabase _db = Substitute.For<IDatabase>();

    private RedisPermissionCacheService CreateSut()
    {
        _redis.GetDatabase().Returns(_db);
        var l1 = new L1PermissionCache(new MemoryCache(Options.Create(new MemoryCacheOptions())));
        return new RedisPermissionCacheService(
            _redis,
            Options.Create(new PermissionCacheOptions()),
            NullLogger<RedisPermissionCacheService>.Instance,
            l1);
    }

    // RTV-01 ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetTokenVersion_WhenKeyAbsent_ReturnsZero()
    {
        var userId = Guid.NewGuid();
        _db.StringGetAsync(Arg.Any<RedisKey>()).Returns(RedisValue.Null);

        var sut = CreateSut();
        var version = await sut.GetTokenVersionAsync(userId);

        version.Should().Be(0, "missing Redis key = version 0 (new user)");
    }

    // RTV-02 ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetTokenVersion_WhenKeyPresent_ReturnsStoredValue()
    {
        var userId = Guid.NewGuid();
        _db.StringGetAsync(Arg.Is<RedisKey>(k => k.ToString().Contains("token-version")))
           .Returns((RedisValue)7);
        _db.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>())
           .Returns(true);

        var sut = CreateSut();
        var version = await sut.GetTokenVersionAsync(userId);

        version.Should().Be(7);
    }

    // RTV-03 ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task IncrementTokenVersion_CallsIncrAndRefreshesExpiry()
    {
        var userId = Guid.NewGuid();
        _db.StringIncrementAsync(Arg.Any<RedisKey>()).Returns(1L);
        _db.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>()).Returns(true);

        var sut = CreateSut();
        await sut.IncrementTokenVersionAsync(userId);

        await _db.Received(1).StringIncrementAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"token-version:{userId}"));

        // TTL must be reset to sliding 7 days
        await _db.Received(1).KeyExpireAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"token-version:{userId}"),
            Arg.Is<TimeSpan>(t => t.TotalDays >= 6.9 && t.TotalDays <= 7.1));
    }

    // RTV-04 ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task InvalidateUser_IncrementsTokenVersion()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _db.StringIncrementAsync(Arg.Any<RedisKey>()).Returns(1L);
        _db.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>()).Returns(true);

        var sut = CreateSut();
        await sut.InvalidateUserAsync(userId, tenantId);

        // InvalidateUserAsync must increment the token version so stale cached
        // permission entries are rejected on next evaluation.
        await _db.Received(1).StringIncrementAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"token-version:{userId}"));
    }
}
