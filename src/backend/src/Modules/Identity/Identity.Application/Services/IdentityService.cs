using Identity.Application.Common;
using Identity.Application.Queries;
using MediatR;

namespace Identity.Application.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly ISender _sender;

    public IdentityService(ISender sender)
        => _sender = sender;

    public Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
        => _sender.Send(new GetUserByIdQuery(userId, Guid.Empty), ct);

    public async Task<UserDto?> GetUserByEmailAsync(
        string email, Guid tenantId, CancellationToken ct = default)
    {
        // Direct cross-module calls bypass tenant-filter so we use TenantId=Guid.Empty
        // and validate membership explicitly in the query handler
        var query = new GetUserByIdQuery(Guid.Empty, tenantId);
        // Route through email-specific query
        return await _sender.Send(
            new GetUserByEmailQuery(email, tenantId), ct);
    }

    public async Task<bool> UserExistsAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var user = await _sender.Send(new GetUserByIdQuery(userId, tenantId), ct);
        return user is not null;
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersByIdsAsync(
        IEnumerable<Guid> ids, Guid tenantId, CancellationToken ct = default)
        => await _sender.Send(new GetUsersByIdsQuery(ids, tenantId), ct);
}
