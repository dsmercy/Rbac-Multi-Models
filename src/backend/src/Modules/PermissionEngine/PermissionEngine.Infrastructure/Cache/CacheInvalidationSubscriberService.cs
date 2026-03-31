using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace PermissionEngine.Infrastructure.Cache;

/// <summary>
/// Background service that subscribes to the Redis pub/sub channel
/// <c>cache-invalidation:*</c> and busts the process-local L1 cache
/// for the affected tenant.
///
/// This completes the distributed invalidation loop:
///   Backend event handler
///     → PublishInvalidationAsync("cache-invalidation:{tenantId}")
///       → all app instances receive the message here
///         → L1PermissionCache.InvalidateTenant(tenantId)
///
/// Redis L2 is invalidated separately by ScanAndDeleteAsync in
/// <see cref="RedisPermissionCacheService"/>.
/// </summary>
public sealed class CacheInvalidationSubscriberService : IHostedService
{
    private readonly IConnectionMultiplexer _mux;
    private readonly L1PermissionCache _l1;
    private readonly ILogger<CacheInvalidationSubscriberService> _logger;

    private static readonly RedisChannel WildcardChannel =
        new("cache-invalidation:*", RedisChannel.PatternMode.Pattern);

    public CacheInvalidationSubscriberService(
        IConnectionMultiplexer mux,
        L1PermissionCache l1,
        ILogger<CacheInvalidationSubscriberService> logger)
    {
        _mux    = mux;
        _l1     = l1;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _mux.GetSubscriber().Subscribe(WildcardChannel, OnInvalidation);
        _logger.LogInformation(
            "L1 cache invalidation subscriber started on channel cache-invalidation:*");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _mux.GetSubscriber().Unsubscribe(WildcardChannel);
        return Task.CompletedTask;
    }

    private void OnInvalidation(RedisChannel channel, RedisValue message)
    {
        if (!Guid.TryParse(message.ToString(), out var tenantId))
        {
            _logger.LogWarning(
                "Received non-GUID payload on invalidation channel {Channel}: {Message}",
                channel, message);
            return;
        }

        _l1.InvalidateTenant(tenantId);

        _logger.LogDebug(
            "L1 cache busted for tenant {TenantId} via pub/sub", tenantId);
    }
}
