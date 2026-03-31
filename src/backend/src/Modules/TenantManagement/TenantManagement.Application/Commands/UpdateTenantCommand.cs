using BuildingBlocks.Application;
using TenantManagement.Application.Common;

namespace TenantManagement.Application.Commands;

public sealed record UpdateTenantCommand(
    Guid TenantId,
    string Name,
    Guid UpdatedByUserId) : ICommand<TenantDto>;
