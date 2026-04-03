using BuildingBlocks.Application;
using MediatR;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Commands;

public sealed class RevokeRoleFromUserCommandHandler
    : ICommandHandler<RevokeRoleFromUserCommand>
{
    private readonly IUserRoleAssignmentRepository _assignmentRepository;
    private readonly IPublisher _publisher;

    public RevokeRoleFromUserCommandHandler(
        IUserRoleAssignmentRepository assignmentRepository,
        IPublisher publisher)
    {
        _assignmentRepository = assignmentRepository;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(
        RevokeRoleFromUserCommand command,
        CancellationToken cancellationToken)
    {
        // When scopeId is provided, look up the exact scoped assignment.
        // When null, search across all scopes to find the first matching active assignment.
        var assignments = command.ScopeId.HasValue
            ? await _assignmentRepository.GetActiveByUserAsync(
                command.UserId, command.TenantId, command.ScopeId, cancellationToken)
            : await _assignmentRepository.GetAllActiveByUserAsync(
                command.UserId, command.TenantId, cancellationToken);

        var target = assignments.FirstOrDefault(a => a.RoleId == command.RoleId);

        if (target is null)
            return Unit.Value; // Idempotent — already revoked

        target.Deactivate("ManualRevocation", command.RevokedByUserId);
        await _assignmentRepository.SaveChangesAsync(cancellationToken);

        foreach (var evt in target.DomainEvents)
            await _publisher.Publish(evt, cancellationToken);

        target.ClearDomainEvents();

        return Unit.Value;
    }
}
