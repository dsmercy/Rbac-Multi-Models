namespace BuildingBlocks.Application;

/// <summary>
/// Provides atomic token-version read/write operations against the distributed cache.
///
/// Design rationale:
///   Defined in BuildingBlocks.Application so Identity.Application can depend on it
///   without introducing a circular reference through PermissionEngine.Domain.
///   Implemented by RedisPermissionCacheService in PermissionEngine.Infrastructure
///   and registered in DI via PermissionEngineModuleExtensions.
///
/// Token version lifecycle:
///   • Initialised to 0 on first read (Redis INCR on a non-existent key returns 1,
///     so we treat "no key" as version 0).
///   • Incremented atomically by: UserRoleAssigned, UserRoleRevoked,
///     DelegationCreated, DelegationRevoked domain-event handlers.
///   • Embedded as the "tv" claim in every issued JWT.
///   • Validated by TokenVersionValidationStep at the start of every permission
///     evaluation pipeline run.
///   • TTL: sliding 7 days — must survive the refresh-token lifetime.
/// </summary>
public interface ITokenVersionService
{
    /// <summary>
    /// Returns the current token version for a user.
    /// Returns 0 if no version key exists (new user, never incremented).
    /// </summary>
    Task<int> GetTokenVersionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Atomically increments the token version.
    /// Any in-flight JWT carrying an older version will be rejected on
    /// the next permission-engine evaluation, forcing re-authentication.
    /// </summary>
    Task IncrementTokenVersionAsync(Guid userId, CancellationToken ct = default);
}
