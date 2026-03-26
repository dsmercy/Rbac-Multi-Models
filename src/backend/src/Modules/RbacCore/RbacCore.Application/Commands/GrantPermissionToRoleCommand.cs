using BuildingBlocks.Application;

namespace RbacCore.Application.Commands;

public sealed record GrantPermissionToRoleCommand(
    Guid TenantId,
    Guid RoleId,
    string PermissionCode,
    Guid GrantedByUserId) : ICommand;
