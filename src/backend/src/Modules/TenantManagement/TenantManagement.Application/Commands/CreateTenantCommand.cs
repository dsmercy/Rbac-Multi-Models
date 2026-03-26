using BuildingBlocks.Application;
using TenantManagement.Application.Common;

namespace TenantManagement.Application.Commands;

public sealed record CreateTenantCommand(
    string Name,
    string Slug,
    string AdminEmail,
    string AdminPassword,
    Guid CreatedByUserId) : ICommand<TenantDto>;
