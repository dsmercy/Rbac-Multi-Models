using BuildingBlocks.Domain;

namespace Identity.Domain.ValueObjects;

public sealed class UserId : ValueObject
{
    public Guid Value { get; }

    private UserId(Guid value) => Value = value;

    public static UserId Create(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("INVALID_USER_ID", "UserId cannot be empty.");

        return new UserId(value);
    }

    public static UserId NewId() => new(Guid.NewGuid());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
