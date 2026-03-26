using BuildingBlocks.Domain;
using System.Text.RegularExpressions;

namespace RbacCore.Domain.ValueObjects;

public sealed class PermissionCode : ValueObject
{
    private static readonly Regex CodePattern =
        new(@"^[a-z0-9]+(?:[-:][a-z0-9]+)*$", RegexOptions.Compiled);

    public string Value { get; }

    private PermissionCode(string value) => Value = value;

    public static PermissionCode Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("INVALID_PERMISSION_CODE", "Permission code cannot be empty.");

        value = value.Trim().ToLowerInvariant();

        if (value.Length > 100)
            throw new DomainException("INVALID_PERMISSION_CODE", "Permission code must not exceed 100 characters.");

        if (!CodePattern.IsMatch(value))
            throw new DomainException("INVALID_PERMISSION_CODE",
                $"Permission code '{value}' is invalid. Use format 'resource:action' (e.g. 'users:read').");

        return new PermissionCode(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
