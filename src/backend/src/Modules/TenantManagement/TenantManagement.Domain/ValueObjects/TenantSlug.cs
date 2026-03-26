using BuildingBlocks.Domain;
using System.Text.RegularExpressions;

namespace TenantManagement.Domain.ValueObjects;

public sealed class TenantSlug : ValueObject
{
    private static readonly Regex SlugPattern = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    public string Value { get; }

    private TenantSlug(string value) => Value = value;

    public static TenantSlug Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("INVALID_SLUG", "Tenant slug cannot be empty.");

        value = value.Trim().ToLowerInvariant();

        if (value.Length < 3 || value.Length > 63)
            throw new DomainException("INVALID_SLUG", "Tenant slug must be between 3 and 63 characters.");

        if (!SlugPattern.IsMatch(value))
            throw new DomainException("INVALID_SLUG", "Tenant slug must contain only lowercase letters, numbers, and hyphens, and cannot start or end with a hyphen.");

        return new TenantSlug(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
