using BuildingBlocks.Domain;

namespace Identity.Domain.ValueObjects;

public sealed class DisplayName : ValueObject
{
    public const int MaxLength = 150;

    public string Value { get; }

    private DisplayName(string value) => Value = value;

    public static DisplayName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("INVALID_DISPLAY_NAME", "Display name cannot be empty.");

        value = value.Trim();

        if (value.Length > MaxLength)
            throw new DomainException(
                "INVALID_DISPLAY_NAME",
                $"Display name cannot exceed {MaxLength} characters.");

        return new DisplayName(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
