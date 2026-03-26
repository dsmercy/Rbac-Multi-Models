using BuildingBlocks.Application;
using Identity.Domain.Interfaces;
using MediatR;
using System.Security.Cryptography;

namespace Identity.Application.Commands;

public sealed class AnonymiseUserCommandHandler : ICommandHandler<AnonymiseUserCommand>
{
    private readonly IUserRepository _userRepository;

    public AnonymiseUserCommandHandler(IUserRepository userRepository)
        => _userRepository = userRepository;

    public async Task<Unit> Handle(
        AnonymiseUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {command.UserId} not found.");

        if (user.TenantId != command.TenantId)
            throw new UnauthorizedAccessException("User does not belong to the specified tenant.");

        var pseudonym = $"[ERASED:{Convert.ToBase64String(RandomNumberGenerator.GetBytes(8))}]";

        user.Anonymise(pseudonym, command.RequestedByUserId);

        await _userRepository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
