using BuildingBlocks.Application;
using Identity.Application.Services;
using MediatR;
using RbacCore.Domain.Entities;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Commands;

public sealed class AssignRoleToUserCommandHandler : ICommandHandler<AssignRoleToUserCommand>
{
    private readonly IUserRoleAssignmentRepository _assignmentRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IIdentityService _identityService;
    private readonly IPublisher _publisher;

    public AssignRoleToUserCommandHandler(
        IUserRoleAssignmentRepository assignmentRepository,
        IRoleRepository roleRepository,
        IIdentityService identityService,
        IPublisher publisher)
    {
        _assignmentRepository = assignmentRepository;
        _roleRepository = roleRepository;
        _identityService = identityService;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(
        AssignRoleToUserCommand command,
        CancellationToken cancellationToken)
    {
        // ACL: verify user exists via Identity module (never join Users table directly)
        var userExists = await _identityService.UserExistsAsync(
            command.UserId, command.TenantId, cancellationToken);

        if (!userExists)
            throw new KeyNotFoundException($"User {command.UserId} not found in tenant {command.TenantId}.");

        var role = await _roleRepository.GetByIdAsync(command.RoleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Role {command.RoleId} not found.");

        if (role.TenantId != command.TenantId)
            throw new UnauthorizedAccessException("Role does not belong to the specified tenant.");

        // Idempotency guard
        var alreadyAssigned = await _assignmentRepository.AssignmentExistsAsync(
            command.UserId, command.RoleId, command.TenantId, command.ScopeId, cancellationToken);

        if (alreadyAssigned)
            return Unit.Value;

        var assignment = UserRoleAssignment.Create(
            command.TenantId,
            command.UserId,
            command.RoleId,
            command.ScopeId,
            command.ExpiresAt,
            command.AssignedByUserId);

        await _assignmentRepository.AddAsync(assignment, cancellationToken);
        await _assignmentRepository.SaveChangesAsync(cancellationToken);

        foreach (var evt in assignment.DomainEvents)
            await _publisher.Publish(evt, cancellationToken);

        assignment.ClearDomainEvents();

        return Unit.Value;
    }
}
