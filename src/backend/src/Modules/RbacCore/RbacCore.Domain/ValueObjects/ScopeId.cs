using BuildingBlocks.Domain;

namespace RbacCore.Domain.ValueObjects;

public sealed class ScopeId : ValueObject
{
    public Guid Value { get; }

    private ScopeId(Guid value) => Value = value;

    public static ScopeId Create(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("INVALID_SCOPE_ID", "ScopeId cannot be empty.");

        return new ScopeId(value);
    }

    public static ScopeId NewId() => new(Guid.NewGuid());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
