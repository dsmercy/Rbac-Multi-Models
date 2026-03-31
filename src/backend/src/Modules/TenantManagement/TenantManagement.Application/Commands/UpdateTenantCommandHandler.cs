using BuildingBlocks.Application;
using TenantManagement.Application.Common;
using TenantManagement.Domain.Interfaces;

namespace TenantManagement.Application.Commands;

public sealed class UpdateTenantCommandHandler : ICommandHandler<UpdateTenantCommand, TenantDto>
{
    private readonly ITenantRepository _tenantRepository;

    public UpdateTenantCommandHandler(ITenantRepository tenantRepository)
        => _tenantRepository = tenantRepository;

    public async Task<TenantDto> Handle(UpdateTenantCommand command, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant {command.TenantId} not found.");

        tenant.Rename(command.Name, command.UpdatedByUserId);

        await _tenantRepository.SaveChangesAsync(cancellationToken);

        return TenantMapper.ToDto(tenant);
    }
}
