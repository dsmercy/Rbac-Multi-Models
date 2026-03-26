using BuildingBlocks.Domain;

namespace Delegation.Domain.Events;

public sealed class DelegationRevokedEvent : DomainEvent
{
    public Guid DelegationId { get; }
    public Guid DelegateeId { get; }
    public Guid RevokedByUserId { get; }

    public DelegationRevokedEvent(
        Guid delegationId, Guid tenantId, Guid delegateeId, Guid revokedByUserId)
    {
        DelegationId = delegationId;
        TenantId = tenantId;
        DelegateeId = delegateeId;
        RevokedByUserId = revokedByUserId;
    }
}
