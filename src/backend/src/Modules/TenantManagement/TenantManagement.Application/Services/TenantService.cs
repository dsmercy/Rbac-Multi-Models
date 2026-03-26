using MediatR;
using TenantManagement.Application.Common;
using TenantManagement.Application.Queries;

namespace TenantManagement.Application.Services;

public sealed class TenantService : ITenantService
{
    private readonly ISender _sender;

    public TenantService(ISender sender)
        => _sender = sender;

    public Task<TenantDto?> GetTenantByIdAsync(Guid tenantId, CancellationToken ct = default)
        => _sender.Send(new GetTenantByIdQuery(tenantId), ct);

    public async Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _sender.Send(new GetTenantByIdQuery(tenantId), ct);
        return tenant is not null;
    }

    public async Task<bool> TenantIsActiveAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _sender.Send(new GetTenantByIdQuery(tenantId), ct);
        return tenant?.IsActive ?? false;
    }

    public async Task<TenantConfigDto> GetConfigAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _sender.Send(new GetTenantByIdQuery(tenantId), ct)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found.");
        return tenant.Configuration;
    }
}
