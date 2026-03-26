using BuildingBlocks.Application;

namespace TenantManagement.Application.Commands;

public sealed record UpdateTenantConfigCommand(
    Guid TenantId,
    int MaxDelegationChainDepth,
    int PermissionCacheTtlSeconds,
    int TokenVersionCacheTtlSeconds,
    int MaxUsersAllowed,
    int MaxRolesAllowed,
    Guid RequestedByUserId) : ICommand;
