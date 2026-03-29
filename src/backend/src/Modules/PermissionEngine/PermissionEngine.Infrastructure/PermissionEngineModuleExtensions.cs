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
        // ── Redis ─────────────────────────────────────────────────────────────
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string is required.");

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection));

        services.AddScoped<IPermissionCacheService, RedisPermissionCacheService>();

        // ── Pipeline steps (order enforced by IEvaluationStep.Order) ─────────
        //
        // Step 0 — Token version validation (NEW — Phase 3)
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

        // Step 7 — Default deny (backstop — always fires if nothing else granted access)
        services.AddScoped<IEvaluationStep, DefaultDenyStep>();

        services.AddScoped<IPermissionEngine, PermissionEngineService>();

        return services;
    }
}