using BuildingBlocks.Domain;

namespace Identity.Domain.ValueObjects;

public sealed class Email : ValueObject
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("INVALID_EMAIL", "Email cannot be empty.");

        value = value.Trim().ToLowerInvariant();

        if (!value.Contains('@') || value.Length > 320)
            throw new DomainException("INVALID_EMAIL", $"'{value}' is not a valid email address.");

        return new Email(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
