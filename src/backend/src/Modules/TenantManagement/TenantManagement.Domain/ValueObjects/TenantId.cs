using BuildingBlocks.Domain;

namespace TenantManagement.Domain.ValueObjects;

public sealed class TenantId : ValueObject
{
    public static readonly Guid SystemTenantSentinel = Guid.Empty;

    public Guid Value { get; }

    private TenantId(Guid value) => Value = value;

    public static TenantId Create(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("INVALID_TENANT_ID", "TenantId cannot be the system sentinel (empty Guid). Use TenantId.System for platform operations.");

        return new TenantId(value);
    }

    public static TenantId NewId() => new(Guid.NewGuid());

    public static TenantId System => new(SystemTenantSentinel);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
