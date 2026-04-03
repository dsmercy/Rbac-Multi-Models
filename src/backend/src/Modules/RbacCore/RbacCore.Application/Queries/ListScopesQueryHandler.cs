using BuildingBlocks.Application;
using RbacCore.Application.Common;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Queries;

public sealed class ListScopesQueryHandler
    : IQueryHandler<ListScopesQuery, IReadOnlyList<ScopeDto>>
{
    private readonly IScopeRepository _scopeRepository;

    public ListScopesQueryHandler(IScopeRepository scopeRepository)
        => _scopeRepository = scopeRepository;

    public async Task<IReadOnlyList<ScopeDto>> Handle(
        ListScopesQuery query,
        CancellationToken cancellationToken)
    {
        var scopes = await _scopeRepository.GetAllByTenantAsync(query.TenantId, cancellationToken);

        return scopes
            .Select(s => new ScopeDto(
                s.Id,
                s.TenantId,
                s.Name,
                s.Type.ToString(),
                s.ParentScopeId,
                s.CreatedAt))
            .ToList()
            .AsReadOnly();
    }
}
