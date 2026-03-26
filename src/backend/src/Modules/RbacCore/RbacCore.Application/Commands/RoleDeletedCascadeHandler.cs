using MediatR;
using RbacCore.Domain.Events;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Application.Commands;

/// <summary>
/// Handles cascade deactivation of all active UserRoleAssignments when a role is deleted.
/// Also busts the permission cache for all affected users via the domain event pipeline.
/// </summary>
public sealed class RoleDeletedCascadeHandler : INotificationHandler<RoleDeletedEvent>
{
    private readonly IUserRoleAssignmentRepository _assignmentRepository;
    private readonly IPublisher _publisher;

    public RoleDeletedCascadeHandler(
        IUserRoleAssignmentRepository assignmentRepository,
        IPublisher publisher)
    {
        _assignmentRepository = assignmentRepository;
        _publisher = publisher;
    }

    public async Task Handle(RoleDeletedEvent notification, CancellationToken cancellationToken)
    {
        var activeAssignments = await _assignmentRepository.GetActiveByRoleAsync(
            notification.RoleId, notification.TenantId, cancellationToken);

        foreach (var assignment in activeAssignments)
        {
            assignment.Deactivate("RoleDeleted", notification.DeletedByUserId);

            // Publish UserRoleRevokedEvent so cache is busted per user
            foreach (var evt in assignment.DomainEvents)
                await _publisher.Publish(evt, cancellationToken);

            assignment.ClearDomainEvents();
        }

        await _assignmentRepository.SaveChangesAsync(cancellationToken);
    }
}
