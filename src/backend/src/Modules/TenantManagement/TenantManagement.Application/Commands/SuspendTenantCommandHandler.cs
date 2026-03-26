using BuildingBlocks.Application;
using MediatR;
using TenantManagement.Domain.Interfaces;

namespace TenantManagement.Application.Commands;

public sealed class SuspendTenantCommandHandler : ICommandHandler<SuspendTenantCommand>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IPublisher _publisher;

    public SuspendTenantCommandHandler(
        ITenantRepository tenantRepository,
        IPublisher publisher)
    {
        _tenantRepository = tenantRepository;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(
        SuspendTenantCommand command,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant {command.TenantId} not found.");

        tenant.Suspend(command.Reason, command.RequestedByUserId);

        await _tenantRepository.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in tenant.DomainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);

        tenant.ClearDomainEvents();

        return Unit.Value;
    }
}
