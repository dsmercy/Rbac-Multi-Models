using BuildingBlocks.Application;
using MediatR;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Commands;

public sealed class DeleteRoleCommandHandler : ICommandHandler<DeleteRoleCommand>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPublisher _publisher;

    public DeleteRoleCommandHandler(
        IRoleRepository roleRepository,
        IPublisher publisher)
    {
        _roleRepository = roleRepository;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(
        DeleteRoleCommand command,
        CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(command.RoleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Role {command.RoleId} not found.");

        if (role.TenantId != command.TenantId)
            throw new UnauthorizedAccessException("Role does not belong to the specified tenant.");

        role.SoftDelete(command.DeletedByUserId);

        await _roleRepository.SaveChangesAsync(cancellationToken);

        // RoleDeletedEvent triggers cascade deactivation of all assignments
        foreach (var evt in role.DomainEvents)
            await _publisher.Publish(evt, cancellationToken);

        role.ClearDomainEvents();

        return Unit.Value;
    }
}
