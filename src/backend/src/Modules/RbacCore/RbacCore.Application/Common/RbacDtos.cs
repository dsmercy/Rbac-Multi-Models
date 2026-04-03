namespace RbacCore.Application.Common;

public sealed record RoleDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    bool IsSystemRole,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PermissionDto> Permissions);

public sealed record PermissionDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string ResourceType,
    string Action,
    string? Description);

public sealed record ScopeDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Type,        // "Organization" | "Department" | "Project" | "Custom"
    Guid? ParentId,
    DateTimeOffset CreatedAt);

public sealed record UserRoleAssignmentDto(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    Guid RoleId,
    string RoleName,
    Guid? ScopeId,
    bool IsActive,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);
