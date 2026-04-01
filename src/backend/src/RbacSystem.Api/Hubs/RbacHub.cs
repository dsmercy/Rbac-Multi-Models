using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RbacSystem.Api.Hubs;

/// <summary>
/// SignalR hub for real-time RBAC invalidation events.
///
/// Route: /api/v1/hubs/rbac
///
/// Tenant isolation:
///   On connect, the authenticated user's "tid" JWT claim is used to place the
///   connection into a SignalR group named "tenant:{tenantId}". All push messages
///   are sent to that group, so clients in tenant A never receive events for tenant B.
///
/// Authentication:
///   Browsers cannot set Authorization headers on WebSocket upgrades.
///   SignalR clients must pass the JWT as a query parameter: ?access_token=...
///   Program.cs configures JwtBearerEvents.OnMessageReceived to extract this.
///
/// Client event: "rbac:invalidated"
///   Payload: RbacInvalidatedMessage { Type, TenantId, ResourceId, OccurredAt }
///   React client refetches affected data and re-evaluates cached permission checks.
///
/// Super-admin:
///   Super-admin connections (is_super_admin=true) join a special group
///   "tenant:platform" in addition to any tenant group, so platform-level events
///   (TenantCreated, TenantSuspended) can be pushed without a real tid.
/// </summary>
[Authorize]
public sealed class RbacHub : Hub
{
    private const string TenantGroupPrefix   = "tenant:";
    private const string PlatformGroup       = "tenant:platform";
    private const string SuperAdminClaimName = "is_super_admin";

    public override async Task OnConnectedAsync()
    {
        var tenantId    = Context.User?.FindFirst("tid")?.Value;
        var isSuperAdmin = Context.User?.FindFirst(SuperAdminClaimName)?.Value
                                  ?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

        if (!string.IsNullOrWhiteSpace(tenantId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{TenantGroupPrefix}{tenantId}");

        if (isSuperAdmin)
            await Groups.AddToGroupAsync(Context.ConnectionId, PlatformGroup);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // SignalR removes the connection from all groups automatically on disconnect;
        // explicit removal is not required but is kept for clarity and testability.
        var tenantId     = Context.User?.FindFirst("tid")?.Value;
        var isSuperAdmin = Context.User?.FindFirst(SuperAdminClaimName)?.Value
                                   ?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

        if (!string.IsNullOrWhiteSpace(tenantId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{TenantGroupPrefix}{tenantId}");

        if (isSuperAdmin)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, PlatformGroup);

        await base.OnDisconnectedAsync(exception);
    }
}
