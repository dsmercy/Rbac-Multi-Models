namespace PermissionEngine.Domain.Exceptions;

/// <summary>
/// Thrown by the cache infrastructure when the backing store (Redis) is
/// unreachable AND the deployment is configured as fail-closed.
///
/// PermissionEngineService catches this to return Denied(RedisUnavailable)
/// without depending on StackExchange.Redis directly.
/// </summary>
public sealed class CacheUnavailableException : Exception
{
    public CacheUnavailableException(string message, Exception inner)
        : base(message, inner) { }
}
