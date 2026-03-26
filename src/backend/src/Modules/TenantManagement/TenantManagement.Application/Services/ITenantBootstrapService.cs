namespace TenantManagement.Application.Services;

public interface ITenantBootstrapService
{
    /// <summary>
    /// Seeds the default admin user, tenant-admin role, and default permissions
    /// for a newly created tenant. Idempotent — safe to call multiple times.
    /// </summary>
    Task BootstrapAsync(Guid tenantId, string adminEmail, string adminPassword, CancellationToken ct = default);
}
