namespace BuildingBlocks.Domain.Events;

/// <summary>
/// Published by Delegation module when a delegation is created.
/// Consumed cross-module by: PermissionEngine (token version), AuditLogging.
/// </summary>
public sealed class DelegationCreatedEvent : DomainEvent
{
    public Guid                    DelegationId    { get; }
    public Guid                    DelegatorId     { get; }
    public Guid                    DelegateeId     { get; }
    public IReadOnlyList<string>   PermissionCodes { get; }
    public Guid                    ScopeId         { get; }
    public DateTimeOffset          ExpiresAt       { get; }
    public int                     ChainDepth      { get; }

    public DelegationCreatedEvent(
        Guid delegationId,
        Guid tenantId,
        Guid delegatorId,
        Guid delegateeId,
        IReadOnlyList<string> permissionCodes,
        Guid scopeId,
        DateTimeOffset expiresAt,
        int chainDepth)
    {
        DelegationId    = delegationId;
        TenantId        = tenantId;
        DelegatorId     = delegatorId;
        DelegateeId     = delegateeId;
        PermissionCodes = permissionCodes;
        ScopeId         = scopeId;
        ExpiresAt       = expiresAt;
        ChainDepth      = chainDepth;
    }
}
