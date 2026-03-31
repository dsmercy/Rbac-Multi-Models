using BuildingBlocks.Application;
using MediatR;
using RbacCore.Application.Common;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Commands;

public sealed class UpdateRoleCommandHandler : ICommandHandler<UpdateRoleCommand, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPublisher _publisher;

    public UpdateRoleCommandHandler(IRoleRepository roleRepository, IPublisher publisher)
    {
        _roleRepository = roleRepository;
        _publisher = publisher;
    }

    public async Task<RoleDto> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(command.RoleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Role {command.RoleId} not found.");

        if (role.TenantId != command.TenantId)
            throw new UnauthorizedAccessException("Role does not belong to the specified tenant.");

        var existing = await _roleRepository.GetByNameAsync(command.Name, command.TenantId, cancellationToken);
        if (existing is not null && existing.Id != command.RoleId)
            throw new InvalidOperationException($"A role named '{command.Name}' already exists in this tenant.");

        role.Rename(command.Name, command.Description, command.UpdatedByUserId);

        await _roleRepository.SaveChangesAsync(cancellationToken);

        foreach (var evt in role.DomainEvents)
            await _publisher.Publish(evt, cancellationToken);

        role.ClearDomainEvents();

        return new RoleDto(
            role.Id, role.TenantId, role.Name, role.Description,
            role.IsSystemRole, role.CreatedAt, []);
    }
}
