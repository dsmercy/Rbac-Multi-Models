using BuildingBlocks.Application;
using PolicyEngine.Domain.Entities;

namespace PolicyEngine.Application.Commands;

public sealed record CreatePolicyCommand(
    Guid TenantId,
    string Name,
    string? Description,
    PolicyEffect Effect,
    string ConditionTreeJson,
    Guid? ResourceId,
    string? Action,
    Guid CreatedByUserId) : ICommand<Guid>;
