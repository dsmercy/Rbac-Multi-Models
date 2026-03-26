using BuildingBlocks.Application;
using Identity.Application.Common;
using Identity.Domain.Interfaces;

namespace Identity.Application.Queries;

public sealed class GetUserByEmailQueryHandler
    : IQueryHandler<GetUserByEmailQuery, UserDto?>
{
    private readonly IUserRepository _userRepository;

    public GetUserByEmailQueryHandler(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<UserDto?> Handle(
        GetUserByEmailQuery query,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(
            query.Email, query.TenantId, cancellationToken);

        return user is null ? null : UserMapper.ToDto(user);
    }
}
