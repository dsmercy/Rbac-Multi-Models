using BuildingBlocks.Application;
using Identity.Application.Common;
using Identity.Domain.Interfaces;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Commands;

public sealed class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;

    public UpdateUserCommandHandler(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<UserDto> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {command.UserId} not found.");

        if (user.TenantId != command.TenantId)
            throw new UnauthorizedAccessException("User does not belong to the specified tenant.");

        var displayName = DisplayName.Create(command.DisplayName);
        user.UpdateProfile(displayName, command.UpdatedByUserId);

        await _userRepository.SaveChangesAsync(cancellationToken);

        return UserMapper.ToDto(user);
    }
}
