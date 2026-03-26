using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace BuildingBlocks.Infrastructure;

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User.FindFirst("tid")?.Value;

            if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var tid))
                throw new InvalidOperationException("TenantId claim is missing or invalid.");

            return tid;
        }
    }

    public bool IsSuperAdmin
        => _httpContextAccessor.HttpContext?
            .User.IsInRole("platform:super-admin") ?? false;

    public Guid UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var uid))
                throw new InvalidOperationException("UserId claim is missing or invalid.");

            return uid;
        }
    }
}
