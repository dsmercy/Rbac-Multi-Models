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
        // Redis
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string is required.");

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection));

        services.AddScoped<IPermissionCacheService, RedisPermissionCacheService>();

        // Register all pipeline steps — order enforced by IEvaluationStep.Order
        services.AddScoped<IEvaluationStep, GlobalDenyStep>();
        services.AddScoped<IEvaluationStep, ResourceLevelOverrideStep>();
        services.AddScoped<IEvaluationStep, DelegationCheckStep>();
        services.AddScoped<IEvaluationStep, ScopeInheritanceStep>();
        services.AddScoped<IEvaluationStep, AbacPolicyStep>();
        services.AddScoped<IEvaluationStep, RbacPermissionCheckStep>();
        services.AddScoped<IEvaluationStep, DefaultDenyStep>();

        services.AddScoped<IPermissionEngine, PermissionEngineService>();

        return services;
    }
}
