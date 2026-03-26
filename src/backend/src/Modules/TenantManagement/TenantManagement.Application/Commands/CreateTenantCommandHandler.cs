using BuildingBlocks.Application;
using MediatR;
using TenantManagement.Application.Common;
using TenantManagement.Application.Services;
using TenantManagement.Domain.Entities;
using TenantManagement.Domain.Interfaces;
using TenantManagement.Domain.ValueObjects;

namespace TenantManagement.Application.Commands;

public sealed class CreateTenantCommandHandler
    : ICommandHandler<CreateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantBootstrapService _bootstrapService;
    private readonly IPublisher _publisher;

    public CreateTenantCommandHandler(
        ITenantRepository tenantRepository,
        ITenantBootstrapService bootstrapService,
        IPublisher publisher)
    {
        _tenantRepository = tenantRepository;
        _bootstrapService = bootstrapService;
        _publisher = publisher;
    }

    public async Task<TenantDto> Handle(
        CreateTenantCommand command,
        CancellationToken cancellationToken)
    {
        var slug = TenantSlug.Create(command.Slug);

        if (await _tenantRepository.SlugExistsAsync(slug.Value, cancellationToken))
            throw new InvalidOperationException(
                $"A tenant with slug '{slug.Value}' already exists.");

        var tenant = Tenant.Create(command.Name, slug, command.CreatedByUserId);

        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _tenantRepository.SaveChangesAsync(cancellationToken);

        // Bootstrap: seed admin user + tenant-admin role + default permissions.
        // Runs in the same request so first login is immediately possible.
        await _bootstrapService.BootstrapAsync(
            tenant.Id,
            command.AdminEmail,
            command.AdminPassword,
            cancellationToken);

        foreach (var domainEvent in tenant.DomainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);

        tenant.ClearDomainEvents();

        return TenantMapper.ToDto(tenant);
    }
}
