using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Events;
using Identity.Domain.Events;
using Identity.Domain.ValueObjects;

namespace Identity.Domain.Entities;

public sealed class User : AuditableEntity
{
    public Guid TenantId { get; private set; }
    public Email Email { get; private set; } = null!;
    public DisplayName DisplayName { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public string? AnonymisedMarker { get; private set; }

    // EF Core constructor
    private User() { }

    public static User Create(
        Guid tenantId,
        Email email,
        DisplayName displayName,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("INVALID_TENANT_ID", "TenantId cannot be empty.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            DisplayName = displayName,
            IsActive = true,
            CreatedBy = createdByUserId
        };

        user.AddDomainEvent(new UserCreatedEvent(
            user.Id,
            tenantId,
            email.Value,
            displayName.Value));

        return user;
    }

    public void ChangeEmail(Email newEmail, Guid changedByUserId)
    {
        if (IsDeleted)
            throw new DomainException("USER_DELETED", "Cannot change email on a deleted user.");

        var oldEmail = Email.Value;
        Email = newEmail;
        SetUpdated(changedByUserId);

        AddDomainEvent(new UserEmailChangedEvent(
            Id,
            TenantId,
            oldEmail,
            newEmail.Value));
    }

    public void Deactivate(Guid deactivatedByUserId, string reason)
    {
        if (!IsActive)
            throw new DomainException("USER_ALREADY_INACTIVE", "User is already inactive.");

        IsActive = false;
        SetUpdated(deactivatedByUserId);

        AddDomainEvent(new UserDeactivatedEvent(Id, TenantId, reason));
    }

    public void Reactivate(Guid reactivatedByUserId)
    {
        if (IsActive)
            throw new DomainException("USER_ALREADY_ACTIVE", "User is already active.");

        if (IsDeleted)
            throw new DomainException("USER_DELETED", "Cannot reactivate a deleted user.");

        IsActive = true;
        SetUpdated(reactivatedByUserId);

        AddDomainEvent(new UserReactivatedEvent(Id, TenantId));
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
    }

    public void Anonymise(string pseudonymToken, Guid requestedByUserId)
    {
        if (AnonymisedMarker is not null)
            throw new DomainException("USER_ALREADY_ANONYMISED", "User has already been anonymised.");

        Email = Email.Create($"{pseudonymToken}@erased.invalid");
        DisplayName = DisplayName.Create($"[ERASED]");
        AnonymisedMarker = pseudonymToken;
        IsActive = false;
        SetUpdated(requestedByUserId);
    }

    public void SoftDelete(Guid deletedByUserId)
        => MarkDeleted(deletedByUserId);
}
