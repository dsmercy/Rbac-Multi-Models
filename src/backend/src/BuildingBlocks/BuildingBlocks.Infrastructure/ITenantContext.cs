namespace BuildingBlocks.Infrastructure;

public interface ITenantContext
{
    Guid TenantId { get; }
    bool IsSuperAdmin { get; }
    Guid UserId { get; }
}
