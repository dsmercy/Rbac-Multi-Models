using BuildingBlocks.Application;
using Identity.Application.Common;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Interfaces;
using Identity.Domain.ValueObjects;
using MediatR;

namespace Identity.Application.Commands;

public sealed class CreateUserCommandHandler
    : ICommandHandler<CreateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserCredentialRepository _credentialRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPublisher _publisher;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IUserCredentialRepository credentialRepository,
        IPasswordHasher passwordHasher,
        IPublisher publisher)
    {
        _userRepository = userRepository;
        _credentialRepository = credentialRepository;
        _passwordHasher = passwordHasher;
        _publisher = publisher;
    }

    public async Task<UserDto> Handle(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await _userRepository.GetByEmailAsync(
            command.Email, command.TenantId, cancellationToken);

        if (existing is not null)
            throw new InvalidOperationException(
                $"A user with email '{command.Email}' already exists in this tenant.");

        var email = Email.Create(command.Email);
        var displayName = DisplayName.Create(command.DisplayName);

        var user = User.Create(
            command.TenantId,
            email,
            displayName,
            command.CreatedByUserId);

        var (hash, salt) = _passwordHasher.HashPassword(command.Password);

        var credential = UserCredential.Create(
            user.Id,
            command.TenantId,
            hash,
            salt);

        await _userRepository.AddAsync(user, cancellationToken);
        await _credentialRepository.AddAsync(credential, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in user.DomainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);

        user.ClearDomainEvents();

        return UserMapper.ToDto(user);
    }
}
