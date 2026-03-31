using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Events;
using TenantManagement.Domain.Events;
using TenantManagement.Domain.ValueObjects;

namespace TenantManagement.Domain.Entities;

public sealed class Tenant : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public TenantSlug Slug { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public bool IsBootstrapped { get; private set; }
    public TenantConfiguration Configuration { get; private set; } = null!;
    public DateTimeOffset? SuspendedAt { get; private set; }
    public string? SuspensionReason { get; private set; }

    // EF Core constructor
    private Tenant() { }

    public static Tenant Create(
        string name,
        TenantSlug slug,
        Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("INVALID_TENANT_NAME", "Tenant name cannot be empty.");

        if (name.Length > 200)
            throw new DomainException("INVALID_TENANT_NAME", "Tenant name must not exceed 200 characters.");

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug,
            IsActive = true,
            IsBootstrapped = false,
            Configuration = TenantConfiguration.CreateDefault(),
            CreatedBy = createdByUserId
        };

        tenant.AddDomainEvent(new TenantCreatedEvent(
            tenant.Id,
            tenant.Name,
            slug.Value,
            createdByUserId));

        return tenant;
    }

    public void MarkBootstrapped(Guid adminUserId)
    {
        if (IsBootstrapped)
            throw new DomainException("ALREADY_BOOTSTRAPPED", "Tenant has already been bootstrapped.");

        IsBootstrapped = true;

        AddDomainEvent(new TenantBootstrapCompletedEvent(Id, adminUserId));
    }

    public void Suspend(string reason, Guid suspendedByUserId)
    {
        if (!IsActive)
            throw new DomainException("TENANT_ALREADY_SUSPENDED", "Tenant is already suspended.");

        IsActive = false;
        SuspendedAt = DateTimeOffset.UtcNow;
        SuspensionReason = reason;
        SetUpdated(suspendedByUserId);

        AddDomainEvent(new TenantSuspendedEvent(Id, reason, suspendedByUserId));
    }

    public void Reactivate(Guid reactivatedByUserId)
    {
        if (IsActive)
            throw new DomainException("TENANT_ALREADY_ACTIVE", "Tenant is already active.");

        IsActive = true;
        SuspendedAt = null;
        SuspensionReason = null;
        SetUpdated(reactivatedByUserId);

        AddDomainEvent(new TenantReactivatedEvent(Id, reactivatedByUserId));
    }

    public void UpdateConfiguration(TenantConfiguration config, Guid updatedByUserId)
    {
        Configuration = config;
        SetUpdated(updatedByUserId);

        AddDomainEvent(new TenantConfigUpdatedEvent(Id, updatedByUserId));
    }

    public void Rename(string newName, Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("INVALID_TENANT_NAME", "Tenant name cannot be empty.");

        Name = newName.Trim();
        SetUpdated(updatedByUserId);
    }
}
