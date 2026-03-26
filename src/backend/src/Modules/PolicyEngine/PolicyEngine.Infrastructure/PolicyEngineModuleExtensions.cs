using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolicyEngine.Application.Services;
using PolicyEngine.Domain.Interfaces;
using PolicyEngine.Infrastructure.Persistence;
using PolicyEngine.Infrastructure.Persistence.Repositories;

namespace PolicyEngine.Infrastructure;

public static class PolicyEngineModuleExtensions
{
    public static IServiceCollection AddPolicyEngineModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<PolicyDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("PolicyEngine"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_policy", "policy")));

        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<ConditionTreeEvaluator>();
        services.AddScoped<IPolicyEngine, PolicyEngineService>();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(Application.Commands.CreatePolicyCommand).Assembly));

        return services;
    }
}
