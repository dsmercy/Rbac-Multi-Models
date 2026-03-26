using BuildingBlocks.Application;
using Delegation.Domain.Interfaces;
using MediatR;
using RbacCore.Application.Services;
using TenantManagement.Application.Services;

namespace Delegation.Application.Commands;

public sealed class CreateDelegationCommandHandler : ICommandHandler<CreateDelegationCommand, Guid>
{
    private readonly IDelegationRepository _repository;
    private readonly IRbacCoreService _rbacCoreService;
    private readonly ITenantService _tenantService;
    private readonly IPublisher _publisher;

    public CreateDelegationCommandHandler(
        IDelegationRepository repository,
        IRbacCoreService rbacCoreService,
        ITenantService tenantService,
        IPublisher publisher)
    {
        _repository = repository;
        _rbacCoreService = rbacCoreService;
        _tenantService = tenantService;
        _publisher = publisher;
    }

    public async Task<Guid> Handle(
        CreateDelegationCommand command,
        CancellationToken cancellationToken)
    {
        var config = await _tenantService.GetConfigAsync(command.TenantId, cancellationToken);

        // Validate: delegator holds every permission being delegated (at creation time)
        foreach (var code in command.PermissionCodes)
        {
            var holds = await _rbacCoreService.UserHasPermissionAsync(
                command.DelegatorId, code, command.TenantId, command.ScopeId, cancellationToken);

            if (!holds)
                throw new InvalidOperationException(
                    $"Delegator {command.DelegatorId} does not hold permission '{code}' " +
                    $"and cannot delegate it.");
        }

        // Determine chain depth: if delegator received this via delegation, depth increments
        var existingDelegation = await _repository.GetActiveDelegationAsync(
            command.DelegatorId,
            command.PermissionCodes.First(),
            command.ScopeId,
            command.TenantId,
            cancellationToken);

        var chainDepth = existingDelegation is not null
            ? existingDelegation.ChainDepth + 1
            : 1;

        if (chainDepth > config.MaxDelegationChainDepth)
            throw new InvalidOperationException(
                $"Delegation chain depth {chainDepth} exceeds tenant max of " +
                $"{config.MaxDelegationChainDepth}.");

        var delegation = Domain.Entities.DelegationGrant.Create(
            command.TenantId,
            command.DelegatorId,
            command.DelegateeId,
            command.PermissionCodes,
            command.ScopeId,
            command.ExpiresAt,
            chainDepth,
            command.CreatedByUserId);

        await _repository.AddAsync(delegation, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        foreach (var evt in delegation.DomainEvents)
            await _publisher.Publish(evt, cancellationToken);

        delegation.ClearDomainEvents();

        return delegation.Id;
    }
}