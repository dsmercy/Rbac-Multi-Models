using BuildingBlocks.Application;

namespace PolicyEngine.Application.Commands;

public sealed record UpdatePolicyCommand(
    Guid TenantId,
    Guid PolicyId,
    string Name,
    string? Description,
    string ConditionTreeJson,
    string? Action,
    Guid UpdatedByUserId) : ICommand;
