using BuildingBlocks.Application;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PermissionEngine.Application.Pipeline;
using PermissionEngine.Application.Services;
using PermissionEngine.Domain.Interfaces;
using PermissionEngine.Infrastructure.Cache;
using StackExchange.Redis;

namespace PermissionEngine.Infrastructure;

public static class PermissionEngineModuleExtensions
{
    public static IServiceCollection AddPermissionEngineModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Options ───────────────────────────────────────────────────────────
        // PermissionCacheOptions (Infrastructure) — Redis-specific config (fail-closed, stampede lock)
        services.Configure<PermissionCacheOptions>(
            configuration.GetSection(PermissionCacheOptions.SectionName));

        // EvaluationOptions (Application) — eval timeout used by the service
        services.Configure<PermissionEngine.Application.EvaluationOptions>(
            configuration.GetSection(PermissionEngine.Application.EvaluationOptions.SectionName));

        // ── L1 in-process memory cache ────────────────────────────────────────
        // IMemoryCache is a singleton; L1PermissionCache wraps it with per-tenant
        // CancellationTokenSource invalidation, also singleton.
        services.AddMemoryCache();
        services.AddSingleton<L1PermissionCache>(
            sp => new L1PermissionCache(sp.GetRequiredService<IMemoryCache>()));

        // ── Redis ─────────────────────────────────────────────────────────────
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string is required.");

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection));

        // Subscribe to cache-invalidation:* pub/sub to bust L1 on domain events.
        services.AddHostedService<CacheInvalidationSubscriberService>();

        // Register RedisPermissionCacheService as both its cache interface
        // and the BuildingBlocks ITokenVersionService, so:
        //   • PermissionEngine pipeline steps get IPermissionCacheService
        //   • Identity.Application LoginCommandHandler gets ITokenVersionService
        // Both resolve to the same scoped instance within a single request.
        services.AddScoped<RedisPermissionCacheService>();

        services.AddScoped<IPermissionCacheService>(
            sp => sp.GetRequiredService<RedisPermissionCacheService>());

        services.AddScoped<ITokenVersionService>(
            sp => sp.GetRequiredService<RedisPermissionCacheService>());

        // ── MediatR — register all PermissionEngine event handlers ───────────
        // Handlers registered:
        //   Token-version:  UserRoleAssigned, UserRoleRevoked, DelegationCreated, DelegationRevoked
        //   Cache eviction: PolicyCreated, PolicyUpdated, PolicyDeleted, TenantSuspended
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(PermissionEngine.Application.EventHandlers.UserRoleAssignedTokenVersionHandler).Assembly));

        // ── Pipeline steps (order enforced by IEvaluationStep.Order) ─────────
        //
        // Step 0 — Token version validation
        services.AddScoped<IEvaluationStep, TokenVersionValidationStep>();

        // Step 1 — Explicit global deny
        services.AddScoped<IEvaluationStep, GlobalDenyStep>();

        // Step 2 — Resource-level override
        services.AddScoped<IEvaluationStep, ResourceLevelOverrideStep>();

        // Step 3 — Delegation check
        services.AddScoped<IEvaluationStep, DelegationCheckStep>();

        // Step 4 — Scope inheritance resolution
        services.AddScoped<IEvaluationStep, ScopeInheritanceStep>();

        // Step 5 — ABAC policy evaluation
        services.AddScoped<IEvaluationStep, AbacPolicyStep>();

        // Step 6 — Role-based permission check
        services.AddScoped<IEvaluationStep, RbacPermissionCheckStep>();

        // Step 7 — Default deny (backstop — always fires if nothing else granted)
        services.AddScoped<IEvaluationStep, DefaultDenyStep>();

        services.AddScoped<IPermissionEngine, PermissionEngineService>();

        return services;
    }
}
