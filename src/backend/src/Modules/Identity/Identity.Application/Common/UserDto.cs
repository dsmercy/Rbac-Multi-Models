namespace Identity.Application.Common;

public sealed record UserDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);
