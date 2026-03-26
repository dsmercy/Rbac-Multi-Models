using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RbacCore.Application.Services;
using RbacCore.Domain.Interfaces;
using RbacCore.Infrastructure.Persistence;
using RbacCore.Infrastructure.Persistence.Repositories;

namespace RbacCore.Infrastructure;

public static class RbacCoreModuleExtensions
{
    public static IServiceCollection AddRbacCoreModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<RbacDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("RbacCore"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_rbac", "rbac")));

        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IUserRoleAssignmentRepository, UserRoleAssignmentRepository>();
        services.AddScoped<IScopeRepository, ScopeRepository>();
        services.AddScoped<IRbacCoreService, RbacCoreService>();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(Application.Commands.CreateRoleCommand).Assembly));

        return services;
    }
}
