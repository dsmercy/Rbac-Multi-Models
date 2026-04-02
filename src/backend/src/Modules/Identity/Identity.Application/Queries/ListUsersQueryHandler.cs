using BuildingBlocks.Application;
using Identity.Application.Common;
using Identity.Domain.Interfaces;

namespace Identity.Application.Queries;

public sealed class ListUsersQueryHandler
    : IQueryHandler<ListUsersQuery, PagedResult<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public ListUsersQueryHandler(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<PagedResult<UserDto>> Handle(
        ListUsersQuery query,
        CancellationToken cancellationToken)
    {
        var (users, totalCount) = await _userRepository.GetByTenantAsync(
            query.TenantId, query.Search, query.Page, query.PageSize, cancellationToken);

        var items = users.Select(UserMapper.ToDto).ToList().AsReadOnly();

        return new PagedResult<UserDto>(items, totalCount, query.Page, query.PageSize);
    }
}
