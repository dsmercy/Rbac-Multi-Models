namespace BuildingBlocks.Domain;

public abstract class AuditableEntity : SoftDeletableEntity
{
    public DateTimeOffset CreatedAt { get; protected init; } = DateTimeOffset.UtcNow;
    public Guid CreatedBy { get; protected init; }
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public Guid? UpdatedBy { get; protected set; }

    protected void SetUpdated(Guid updatedByUserId)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;
    }
}
