using BuildingBlocks.Application;
using Identity.Domain.Interfaces;
using MediatR;

namespace Identity.Application.Commands;

public sealed class DeactivateUserCommandHandler : ICommandHandler<DeactivateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IPublisher _publisher;

    public DeactivateUserCommandHandler(
        IUserRepository userRepository,
        IPublisher publisher)
    {
        _userRepository = userRepository;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(
        DeactivateUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {command.UserId} not found.");

        if (user.TenantId != command.TenantId)
            throw new UnauthorizedAccessException("User does not belong to the specified tenant.");

        user.Deactivate(command.RequestedByUserId, command.Reason);

        await _userRepository.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in user.DomainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);

        user.ClearDomainEvents();

        return Unit.Value;
    }
}
