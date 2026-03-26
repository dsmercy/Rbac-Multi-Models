namespace BuildingBlocks.Domain;

public abstract class SoftDeletableEntity : Entity
{
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    protected virtual void MarkDeleted(Guid deletedByUserId)
    {
        if (IsDeleted)
            throw new DomainException("ALREADY_DELETED", $"Entity {Id} is already soft-deleted.");

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedBy = deletedByUserId;
    }
}
