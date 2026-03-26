using BuildingBlocks.Application;
using MediatR;
using PolicyEngine.Domain.Interfaces;

namespace PolicyEngine.Application.Commands;

public sealed record DeletePolicyCommand(
    Guid TenantId,
    Guid PolicyId,
    Guid DeletedByUserId) : ICommand;

public sealed class DeletePolicyCommandHandler : ICommandHandler<DeletePolicyCommand>
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IPublisher _publisher;

    public DeletePolicyCommandHandler(IPolicyRepository policyRepository, IPublisher publisher)
    {
        _policyRepository = policyRepository;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(DeletePolicyCommand command, CancellationToken cancellationToken)
    {
        var policy = await _policyRepository.GetByIdAsync(command.PolicyId, cancellationToken)
            ?? throw new KeyNotFoundException($"Policy {command.PolicyId} not found.");

        if (policy.TenantId != command.TenantId)
            throw new UnauthorizedAccessException("Policy does not belong to the specified tenant.");

        policy.SoftDelete(command.DeletedByUserId);

        await _policyRepository.SaveChangesAsync(cancellationToken);

        foreach (var evt in policy.DomainEvents)
            await _publisher.Publish(evt, cancellationToken);

        policy.ClearDomainEvents();

        return Unit.Value;
    }
}
