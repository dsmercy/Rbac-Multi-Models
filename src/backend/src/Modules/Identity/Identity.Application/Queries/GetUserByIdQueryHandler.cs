using BuildingBlocks.Application;
using Identity.Application.Common;
using Identity.Domain.Interfaces;

namespace Identity.Application.Queries;

public sealed class GetUserByIdQueryHandler
    : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    private readonly IUserRepository _userRepository;

    public GetUserByIdQueryHandler(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<UserDto?> Handle(
        GetUserByIdQuery query,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(query.UserId, cancellationToken);

        if (user is null || user.TenantId != query.TenantId)
            return null;

        return UserMapper.ToDto(user);
    }
}
