using BuildingBlocks.Domain;
using Delegation.Domain.Events;

namespace Delegation.Domain.Entities;

public sealed class DelegationGrant : AuditableEntity
{
    public Guid TenantId { get; private set; }
    public Guid DelegatorId { get; private set; }
    public Guid DelegateeId { get; private set; }
    public IReadOnlyList<string> PermissionCodes { get; private set; } = [];
    public Guid ScopeId { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public int ChainDepth { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }

    // EF Core constructor
    private DelegationGrant() { }

    public static DelegationGrant Create(
        Guid tenantId,
        Guid delegatorId,
        Guid delegateeId,
        IReadOnlyList<string> permissionCodes,
        Guid scopeId,
        DateTimeOffset expiresAt,
        int chainDepth,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("INVALID_TENANT_ID", "TenantId cannot be empty.");

        if (delegatorId == delegateeId)
            throw new DomainException("SELF_DELEGATION", "A user cannot delegate to themselves.");

        if (expiresAt <= DateTimeOffset.UtcNow)
            throw new DomainException("INVALID_EXPIRY", "Delegation expiry must be in the future.");

        if (!permissionCodes.Any())
            throw new DomainException("NO_PERMISSIONS", "At least one permission must be delegated.");

        if (chainDepth < 1)
            throw new DomainException("INVALID_CHAIN_DEPTH", "Chain depth must be at least 1.");

        var delegation = new DelegationGrant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DelegatorId = delegatorId,
            DelegateeId = delegateeId,
            PermissionCodes = permissionCodes,
            ScopeId = scopeId,
            ExpiresAt = expiresAt,
            ChainDepth = chainDepth,
            IsRevoked = false,
            CreatedBy = createdByUserId
        };

        delegation.AddDomainEvent(new DelegationCreatedEvent(
            delegation.Id, tenantId, delegatorId, delegateeId,
            permissionCodes, scopeId, expiresAt, chainDepth));

        return delegation;
    }

    public void Revoke(Guid revokedByUserId)
    {
        if (IsRevoked)
            throw new DomainException("ALREADY_REVOKED", "Delegation is already revoked.");

        IsRevoked = true;
        RevokedAt = DateTimeOffset.UtcNow;
        RevokedByUserId = revokedByUserId;
        SetUpdated(revokedByUserId);

        AddDomainEvent(new DelegationRevokedEvent(Id, TenantId, DelegateeId, revokedByUserId));
    }

    public bool IsExpired()
        => ExpiresAt <= DateTimeOffset.UtcNow;

    public bool IsActive()
        => !IsRevoked && !IsExpired();
}
