using BuildingBlocks.Application;
using Identity.Application.Common;
using Identity.Domain.Interfaces;

namespace Identity.Application.Queries;

public sealed class GetUsersByIdsQueryHandler
    : IQueryHandler<GetUsersByIdsQuery, IReadOnlyList<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetUsersByIdsQueryHandler(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<IReadOnlyList<UserDto>> Handle(
        GetUsersByIdsQuery query,
        CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetByIdsAsync(
            query.UserIds, query.TenantId, cancellationToken);

        return users.Select(UserMapper.ToDto).ToList().AsReadOnly();
    }
}
