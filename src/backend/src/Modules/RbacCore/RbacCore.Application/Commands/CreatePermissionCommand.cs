using BuildingBlocks.Application;
using RbacCore.Application.Common;

namespace RbacCore.Application.Commands;

public sealed record CreatePermissionCommand(
    Guid TenantId,
    string Code,
    string ResourceType,
    string Action,
    string? Description,
    Guid CreatedByUserId) : ICommand<PermissionDto>;
