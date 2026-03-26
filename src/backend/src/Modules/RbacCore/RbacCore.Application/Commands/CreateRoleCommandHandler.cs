using BuildingBlocks.Application;
using MediatR;
using RbacCore.Application.Common;
using RbacCore.Domain.Entities;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Commands;

public sealed class CreateRoleCommandHandler : ICommandHandler<CreateRoleCommand, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPublisher _publisher;

    public CreateRoleCommandHandler(IRoleRepository roleRepository, IPublisher publisher)
    {
        _roleRepository = roleRepository;
        _publisher = publisher;
    }

    public async Task<RoleDto> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        var existing = await _roleRepository.GetByNameAsync(
            command.Name, command.TenantId, cancellationToken);

        if (existing is not null)
            throw new InvalidOperationException(
                $"A role named '{command.Name}' already exists in this tenant.");

        var role = Role.Create(
            command.TenantId,
            command.Name,
            command.Description,
            command.CreatedByUserId,
            command.IsSystemRole);

        await _roleRepository.AddAsync(role, cancellationToken);
        await _roleRepository.SaveChangesAsync(cancellationToken);

        foreach (var evt in role.DomainEvents)
            await _publisher.Publish(evt, cancellationToken);

        role.ClearDomainEvents();

        return new RoleDto(
            role.Id, role.TenantId, role.Name, role.Description,
            role.IsSystemRole, role.CreatedAt, []);
    }
}
