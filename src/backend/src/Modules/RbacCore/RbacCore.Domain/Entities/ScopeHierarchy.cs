using BuildingBlocks.Domain;

namespace RbacCore.Domain.Entities;

/// <summary>
/// Closure table for arbitrary-depth scope hierarchy.
/// Each row records an ancestor→descendant relationship at a given depth.
/// Self-reference row (depth=0) is always present for every scope.
///
/// Why closure table over ltree:
///   - Works on both PostgreSQL and Azure SQL without extensions
///   - Efficient ancestor/descendant queries with a single indexed join
///   - Depth column enables max-depth enforcement for delegation chains
/// </summary>
public sealed class ScopeHierarchy : Entity
{
    public Guid TenantId { get; private set; }
    public Guid AncestorId { get; private set; }
    public Guid DescendantId { get; private set; }
    public int Depth { get; private set; }

    // EF Core constructor
    private ScopeHierarchy() { }

    public static ScopeHierarchy Create(
        Guid tenantId,
        Guid ancestorId,
        Guid descendantId,
        int depth)
    {
        if (depth < 0)
            throw new DomainException("INVALID_DEPTH", "Hierarchy depth cannot be negative.");

        return new ScopeHierarchy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AncestorId = ancestorId,
            DescendantId = descendantId,
            Depth = depth
        };
    }
}
