using BuildingBlocks.Application;
using TenantManagement.Application.Common;
using TenantManagement.Domain.Interfaces;

namespace TenantManagement.Application.Queries;

public sealed class GetTenantByIdQueryHandler
    : IQueryHandler<GetTenantByIdQuery, TenantDto?>
{
    private readonly ITenantRepository _tenantRepository;

    public GetTenantByIdQueryHandler(ITenantRepository tenantRepository)
        => _tenantRepository = tenantRepository;

    public async Task<TenantDto?> Handle(
        GetTenantByIdQuery query,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(query.TenantId, cancellationToken);
        return tenant is null ? null : TenantMapper.ToDto(tenant);
    }
}
