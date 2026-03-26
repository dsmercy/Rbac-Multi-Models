using BuildingBlocks.Application;
using MediatR;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Commands;

public sealed class GrantPermissionToRoleCommandHandler
    : ICommandHandler<GrantPermissionToRoleCommand>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IPublisher _publisher;

    public GrantPermissionToRoleCommandHandler(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IPublisher publisher)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(
        GrantPermissionToRoleCommand command,
        CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(command.RoleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Role {command.RoleId} not found.");

        if (role.TenantId != command.TenantId)
            throw new UnauthorizedAccessException("Role does not belong to the specified tenant.");

        var permission = await _permissionRepository.GetByCodeAsync(
            command.PermissionCode, command.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Permission '{command.PermissionCode}' not found.");

        role.AddPermission(permission.Id, command.GrantedByUserId);

        await _roleRepository.SaveChangesAsync(cancellationToken);

        foreach (var evt in role.DomainEvents)
            await _publisher.Publish(evt, cancellationToken);

        role.ClearDomainEvents();

        return Unit.Value;
    }
}
