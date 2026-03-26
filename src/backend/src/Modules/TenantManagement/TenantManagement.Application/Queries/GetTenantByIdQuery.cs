using BuildingBlocks.Application;
using TenantManagement.Application.Common;

namespace TenantManagement.Application.Queries;

public sealed record GetTenantByIdQuery(Guid TenantId) : IQuery<TenantDto?>;
