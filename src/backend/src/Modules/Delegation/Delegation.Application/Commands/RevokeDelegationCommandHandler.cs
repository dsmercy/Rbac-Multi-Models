using BuildingBlocks.Application;
using Delegation.Domain.Interfaces;
using MediatR;
using PermissionEngine.Domain.Interfaces;

namespace Delegation.Application.Commands;

public sealed class RevokeDelegationCommandHandler : ICommandHandler<RevokeDelegationCommand>
{
    private readonly IDelegationRepository _repository;
    private readonly IPermissionCacheService _cacheService;
    private readonly IPublisher _publisher;

    public RevokeDelegationCommandHandler(
        IDelegationRepository repository,
        IPermissionCacheService cacheService,
        IPublisher publisher)
    {
        _repository = repository;
        _cacheService = cacheService;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(
        RevokeDelegationCommand command,
        CancellationToken cancellationToken)
    {
        var delegation = await _repository.GetByIdAsync(command.DelegationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Delegation {command.DelegationId} not found.");

        if (delegation.TenantId != command.TenantId)
            throw new UnauthorizedAccessException(
                "Delegation does not belong to the specified tenant.");

        delegation.Revoke(command.RevokedByUserId);

        await _repository.SaveChangesAsync(cancellationToken);

        // Immediately bust cache for the delegatee + increment token version
        // so in-flight requests cannot use the revoked delegation
        await _cacheService.InvalidateUserAsync(
            delegation.DelegateeId, command.TenantId, cancellationToken);

        await _cacheService.IncrementTokenVersionAsync(
            delegation.DelegateeId, cancellationToken);

        foreach (var evt in delegation.DomainEvents)
            await _publisher.Publish(evt, cancellationToken);

        delegation.ClearDomainEvents();

        return Unit.Value;
    }
}
