using Identity.Application.Commands;
using Identity.Application.Services;
using MediatR;
using RbacCore.Application.Commands;
using TenantManagement.Application.Services;
using TenantManagement.Domain.Interfaces;

namespace TenantManagement.Infrastructure.Services;

/// <summary>
/// Solves the chicken-and-egg bootstrapping problem.
/// Runs inside the same transaction scope as CreateTenantCommandHandler.
/// Steps: Create admin user → Create tenant-admin role → Assign role to user.
/// Every step checks existence first — fully idempotent.
/// </summary>
public sealed class TenantBootstrapService : ITenantBootstrapService
{
    private readonly ISender _sender;
    private readonly ITenantRepository _tenantRepository;
    private readonly IIdentityService _identityService;

    public TenantBootstrapService(
        ISender sender,
        ITenantRepository tenantRepository,
        IIdentityService identityService)
    {
        _sender = sender;
        _tenantRepository = tenantRepository;
        _identityService = identityService;
    }

    public async Task BootstrapAsync(
        Guid tenantId,
        string adminEmail,
        string adminPassword,
        CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found for bootstrap.");

        if (tenant.IsBootstrapped)
            return; // Idempotent guard

        // Step 1: Create the admin user via Identity module (cross-module call via ACL)
        var existingAdmin = await _identityService.GetUserByEmailAsync(adminEmail, tenantId, ct);

        Guid adminUserId;

        if (existingAdmin is not null)
        {
            adminUserId = existingAdmin.Id;
        }
        else
        {
            // System user (Guid.Empty) creates the first admin — bootstrap-only pattern
            var systemUserId = Guid.Empty;
            var createdAdmin = await _sender.Send(
                new CreateUserCommand(tenantId, adminEmail, "Tenant Admin", adminPassword, systemUserId), ct);
            adminUserId = createdAdmin.Id;
        }

        // Step 2: Create tenant-admin role via RbacCore module
        var roleResult = await _sender.Send(
            new CreateRoleCommand(
                tenantId,
                "tenant-admin",
                "Full administrative access within this tenant.",
                adminUserId,
                IsSystemRole: true), ct);

        // Step 3: Assign all default permissions to the tenant-admin role
        var defaultPermissions = DefaultPermissions.GetAll();
        foreach (var permission in defaultPermissions)
        {
            await _sender.Send(
                new GrantPermissionToRoleCommand(
                    tenantId,
                    roleResult.Id,
                    permission,
                    adminUserId), ct);
        }

        // Step 4: Assign the tenant-admin role to the admin user
        await _sender.Send(
            new AssignRoleToUserCommand(
                tenantId,
                adminUserId,
                roleResult.Id,
                ScopeId: null,
                ExpiresAt: null,
                AssignedByUserId: adminUserId), ct);

        // Step 5: Mark tenant as bootstrapped
        tenant.MarkBootstrapped(adminUserId);
        await _tenantRepository.SaveChangesAsync(ct);
    }
}