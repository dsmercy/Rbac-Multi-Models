using Delegation.Application.Services;
using Delegation.Domain.Interfaces;
using Delegation.Infrastructure.Persistence;
using Delegation.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Delegation.Infrastructure;

public static class DelegationModuleExtensions
{
    public static IServiceCollection AddDelegationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<DelegationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Delegation"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_delegation", "delegation")));

        services.AddScoped<IDelegationRepository, DelegationRepository>();
        services.AddScoped<IDelegationService, DelegationService>();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(Application.Commands.CreateDelegationCommand).Assembly));

        return services;
    }
}
