using PolicyEngine.Domain.Entities;

namespace PolicyEngine.Application.Common;

public sealed record PolicyDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    PolicyEffect Effect,
    string ConditionTreeJson,
    Guid? ResourceId,
    string? Action,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
