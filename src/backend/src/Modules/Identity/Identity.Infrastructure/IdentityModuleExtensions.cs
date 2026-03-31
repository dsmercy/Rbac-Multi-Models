using BuildingBlocks.Application;
using Identity.Application.Services;
using Identity.Domain.Interfaces;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Repositories;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IdentityService = Identity.Infrastructure.Services.IdentityService;

namespace Identity.Infrastructure;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Identity"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_identity", "identity")));

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Services
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();

        // IUserRoleProvider is registered in Program.cs AFTER this module
        // because UserRoleProvider queries the RbacCore schema via Dapper.
        // This ordering avoids a project reference from Identity.Infrastructure
        // to RbacCore.Infrastructure.
        //
        // ITokenVersionService is provided by PermissionEngineModuleExtensions
        // (registered as RedisPermissionCacheService implementing both interfaces).
        // PermissionEngineModule must be registered before IdentityModule so the
        // ITokenVersionService registration is available when LoginCommandHandler
        // is resolved. Program.cs enforces this ordering.

        // MediatR handlers for this module
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(Application.Commands.CreateUserCommand).Assembly));

        return services;
    }
}
