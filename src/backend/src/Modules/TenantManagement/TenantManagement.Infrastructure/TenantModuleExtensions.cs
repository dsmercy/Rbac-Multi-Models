using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TenantManagement.Application.Services;
using TenantManagement.Domain.Interfaces;
using TenantManagement.Infrastructure.Persistence;
using TenantManagement.Infrastructure.Persistence.Repositories;
using TenantManagement.Infrastructure.Services;

namespace TenantManagement.Infrastructure;

public static class TenantModuleExtensions
{
    public static IServiceCollection AddTenantManagementModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<TenantDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("TenantManagement"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_tenant", "tenant")));

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantBootstrapService, TenantBootstrapService>();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(Application.Commands.CreateTenantCommand).Assembly));

        return services;
    }
}
