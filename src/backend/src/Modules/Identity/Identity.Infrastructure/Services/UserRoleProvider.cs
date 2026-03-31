using Dapper;
using Identity.Application.Services;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Identity.Infrastructure.Services;

/// <summary>
/// Concrete implementation of IUserRoleProvider.
///
/// Uses a single Dapper query against the RbacCore schema to:
///   1. Return all active role names for the user (own tenant + platform tenant).
///   2. Return all scope IDs where the user has at least one active assignment.
///   3. Determine if the user is a platform super-admin.
///
/// Uses the RbacCore connection string directly — no EF Core, no JWT,
/// no circular dependency on RbacCore.Application.
/// </summary>
public sealed class UserRoleProvider : IUserRoleProvider
{
    private readonly string _connectionString;

    public UserRoleProvider(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("RbacCore")
            ?? throw new InvalidOperationException("RbacCore connection string is required.");
    }

    /// <inheritdoc />
    public async Task<UserLoginInfo> GetLoginInfoAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        // Single query: return one row per (role name, scope ID) combination.
        // ScopeId may be NULL for tenant-wide assignments.
        const string sql = """
            SELECT
                r."Name"    AS RoleName,
                ura."ScopeId" AS ScopeId
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

        var rows = await conn.QueryAsync<(string RoleName, Guid? ScopeId)>(
            new CommandDefinition(sql, new
            {
                UserId = userId,
                TenantId = tenantId,
                PlatformTenantId = Guid.Empty   // platform:super-admin lives here
            },
            cancellationToken: ct));

        var roleNames = rows
            .Select(r => r.RoleName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();

        var scopeIds = rows
            .Where(r => r.ScopeId.HasValue)
            .Select(r => r.ScopeId!.Value)
            .Distinct()
            .ToList()
            .AsReadOnly();

        // Super-admin check: role name convention is "platform:super-admin"
        var isSuperAdmin = roleNames.Any(n =>
            n.Equals("platform:super-admin", StringComparison.OrdinalIgnoreCase));

        return new UserLoginInfo(roleNames, scopeIds, isSuperAdmin);
    }
}
