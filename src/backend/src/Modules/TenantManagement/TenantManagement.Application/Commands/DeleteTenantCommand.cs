using BuildingBlocks.Application;

namespace TenantManagement.Application.Commands;

public sealed record DeleteTenantCommand(
    Guid TenantId,
    Guid DeletedByUserId) : ICommand;
