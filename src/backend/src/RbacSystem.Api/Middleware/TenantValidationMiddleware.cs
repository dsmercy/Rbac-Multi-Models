using System.Security.Claims;

namespace RbacSystem.Api.Middleware;

/// <summary>
/// Validates that the {tid} route parameter matches the tid claim in the JWT.
/// Rejects with 403 on mismatch — prevents cross-tenant access attempts.
/// Super-admins bypass this check (they carry the system tenant sentinel).
/// </summary>
public sealed class TenantValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantValidationMiddleware> _logger;

    public TenantValidationMiddleware(
        RequestDelegate next,
        ILogger<TenantValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var routeTenantId = context.GetRouteValue("tid")?.ToString();

        if (routeTenantId is not null && context.User.Identity?.IsAuthenticated == true)
        {
            var isSuperAdmin = context.User.IsInRole("platform:super-admin");

            if (!isSuperAdmin)
            {
                var jwtTenantId = context.User.FindFirst("tid")?.Value;

                if (string.IsNullOrWhiteSpace(jwtTenantId) ||
                    !string.Equals(routeTenantId, jwtTenantId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Tenant mismatch: route={RouteTid}, jwt={JwtTid}, user={UserId}",
                        routeTenantId,
                        jwtTenantId,
                        context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        code = "TENANT_MISMATCH",
                        message = "The tenant in the request does not match your authenticated tenant."
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}
