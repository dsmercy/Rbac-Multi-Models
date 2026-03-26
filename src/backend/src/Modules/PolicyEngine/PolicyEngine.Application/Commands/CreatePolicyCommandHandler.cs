using BuildingBlocks.Application;
using MediatR;
using PolicyEngine.Application.Services;
using PolicyEngine.Domain.Entities;
using PolicyEngine.Domain.Interfaces;

namespace PolicyEngine.Application.Commands;

public sealed class CreatePolicyCommandHandler : ICommandHandler<CreatePolicyCommand, Guid>
{
    private readonly IPolicyRepository _policyRepository;
    private readonly ConditionTreeEvaluator _evaluator;
    private readonly IPublisher _publisher;

    public CreatePolicyCommandHandler(
        IPolicyRepository policyRepository,
        ConditionTreeEvaluator evaluator,
        IPublisher publisher)
    {
        _policyRepository = policyRepository;
        _evaluator = evaluator;
        _publisher = publisher;
    }

    public async Task<Guid> Handle(
        CreatePolicyCommand command,
        CancellationToken cancellationToken)
    {
        // Validate condition tree JSON is parseable before persisting
        try
        {
            _evaluator.Evaluate(command.ConditionTreeJson,
                new PermissionEngine.Domain.Models.EvaluationContext(command.TenantId, Guid.NewGuid()));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"ConditionTreeJson is invalid: {ex.Message}", ex);
        }

        var policy = Policy.Create(
            command.TenantId,
            command.Name,
            command.Description,
            command.Effect,
            command.ConditionTreeJson,
            command.ResourceId,
            command.Action,
            command.CreatedByUserId);

        await _policyRepository.AddAsync(policy, cancellationToken);
        await _policyRepository.SaveChangesAsync(cancellationToken);

        foreach (var evt in policy.DomainEvents)
            await _publisher.Publish(evt, cancellationToken);

        policy.ClearDomainEvents();

        return policy.Id;
    }
}
