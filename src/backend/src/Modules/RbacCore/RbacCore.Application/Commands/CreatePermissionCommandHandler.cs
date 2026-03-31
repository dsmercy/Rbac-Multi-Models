using BuildingBlocks.Application;
using MediatR;
using RbacCore.Application.Common;
using RbacCore.Domain.Entities;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Commands;

public sealed class CreatePermissionCommandHandler : ICommandHandler<CreatePermissionCommand, PermissionDto>
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IPublisher _publisher;

    public CreatePermissionCommandHandler(
        IPermissionRepository permissionRepository,
        IPublisher publisher)
    {
        _permissionRepository = permissionRepository;
        _publisher = publisher;
    }

    public async Task<PermissionDto> Handle(
        CreatePermissionCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await _permissionRepository.GetByCodeAsync(
            command.Code, command.TenantId, cancellationToken);

        if (existing is not null)
            throw new InvalidOperationException(
                $"A permission with code '{command.Code}' already exists in this tenant.");

        var permission = Permission.Create(
            command.TenantId,
            command.Code,
            command.ResourceType,
            command.Action,
            command.Description,
            command.CreatedByUserId);

        await _permissionRepository.AddAsync(permission, cancellationToken);
        await _permissionRepository.SaveChangesAsync(cancellationToken);

        return new PermissionDto(
            permission.Id,
            permission.TenantId,
            permission.Code.Value,
            permission.ResourceType,
            permission.Action,
            permission.Description);
    }
}
