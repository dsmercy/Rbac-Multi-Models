using BuildingBlocks.Application;
using MediatR;
using TenantManagement.Domain.Interfaces;

namespace TenantManagement.Application.Commands;

public sealed class DeleteTenantCommandHandler : ICommandHandler<DeleteTenantCommand>
{
    private readonly ITenantRepository _tenantRepository;

    public DeleteTenantCommandHandler(ITenantRepository tenantRepository)
        => _tenantRepository = tenantRepository;

    public async Task<Unit> Handle(DeleteTenantCommand command, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant {command.TenantId} not found.");

        tenant.SoftDelete(command.DeletedByUserId);

        await _tenantRepository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
