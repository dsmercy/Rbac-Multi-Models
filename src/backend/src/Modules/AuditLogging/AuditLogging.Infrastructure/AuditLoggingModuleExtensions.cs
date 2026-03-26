using AuditLogging.Application.Services;
using AuditLogging.Domain.Interfaces;
using AuditLogging.Infrastructure.Persistence;
using AuditLogging.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuditLogging.Infrastructure;

public static class AuditLoggingModuleExtensions
{
    public static IServiceCollection AddAuditLoggingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("AuditLogging"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_audit", "audit")));

        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(Application.EventHandlers.UserCreatedAuditHandler).Assembly));

        return services;
    }
}
