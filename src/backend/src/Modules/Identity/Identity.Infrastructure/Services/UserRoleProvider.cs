// Identity.Infrastructure/Services/UserRoleProvider.cs
using Dapper;
using Identity.Application.Services;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Identity.Infrastructure.Services;

public sealed class UserRoleProvider : IUserRoleProvider
{
    private readonly string _connectionString;

    public UserRoleProvider(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("RbacCore")
            ?? throw new InvalidOperationException("RbacCore connection string is required.");
    }

    public async Task<IReadOnlyList<string>> GetRoleNamesForLoginAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT r."Name"
            FROM rbac."UserRoleAssignments" ura
            JOIN rbac."Roles" r ON r."Id" = ura."RoleId"
            WHERE ura."UserId"   = @UserId
              AND ura."TenantId" IN (@TenantId, @PlatformTenantId)
              AND ura."IsActive" = TRUE
              AND ura."IsDeleted" = FALSE
              AND r."IsDeleted"  = FALSE
              AND (ura."ExpiresAt" IS NULL OR ura."ExpiresAt" > NOW())
            """;

        await using var conn = new NpgsqlConnection(_connectionString);

        var roles = await conn.QueryAsync<string>(
            new CommandDefinition(sql, new
            {
                UserId = userId,
                TenantId = tenantId,
                PlatformTenantId = Guid.Empty   // platform:super-admin lives here
            },
            cancellationToken: ct));

        return roles.Distinct().ToList().AsReadOnly();
    }
}