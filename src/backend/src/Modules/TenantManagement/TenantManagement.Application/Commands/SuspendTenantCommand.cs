using BuildingBlocks.Application;

namespace TenantManagement.Application.Commands;

public sealed record SuspendTenantCommand(
    Guid TenantId,
    string Reason,
    Guid RequestedByUserId) : ICommand;
