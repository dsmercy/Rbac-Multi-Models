using BuildingBlocks.Application;
using MediatR;
using TenantManagement.Domain.Interfaces;
using TenantManagement.Domain.ValueObjects;

namespace TenantManagement.Application.Commands;

public sealed class UpdateTenantConfigCommandHandler
    : ICommandHandler<UpdateTenantConfigCommand>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IPublisher _publisher;

    public UpdateTenantConfigCommandHandler(
        ITenantRepository tenantRepository,
        IPublisher publisher)
    {
        _tenantRepository = tenantRepository;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(
        UpdateTenantConfigCommand command,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant {command.TenantId} not found.");

        var config = TenantConfiguration.Create(
            command.MaxDelegationChainDepth,
            command.PermissionCacheTtlSeconds,
            command.TokenVersionCacheTtlSeconds,
            command.MaxUsersAllowed,
            command.MaxRolesAllowed);

        tenant.UpdateConfiguration(config, command.RequestedByUserId);

        await _tenantRepository.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in tenant.DomainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);

        tenant.ClearDomainEvents();

        return Unit.Value;
    }
}
