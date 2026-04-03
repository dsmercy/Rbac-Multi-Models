using RbacCore.Domain.Entities;

namespace RbacCore.Domain.Interfaces;

public interface IUserRoleAssignmentRepository
{
    Task<IReadOnlyList<UserRoleAssignment>> GetActiveByUserAsync(
        Guid userId, Guid tenantId, Guid? scopeId, CancellationToken ct = default);

    /// <summary>Returns all active assignments for a user across every scope (no scope filter).</summary>
    Task<IReadOnlyList<UserRoleAssignment>> GetAllActiveByUserAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<UserRoleAssignment>> GetActiveByRoleAsync(
        Guid roleId, Guid tenantId, CancellationToken ct = default);

    Task<UserRoleAssignment?> GetByIdAsync(
        Guid assignmentId, CancellationToken ct = default);

    Task<bool> AssignmentExistsAsync(
        Guid userId, Guid roleId, Guid tenantId, Guid? scopeId, CancellationToken ct = default);

    Task AddAsync(UserRoleAssignment assignment, CancellationToken ct = default);
 
    Task SaveChangesAsync(CancellationToken ct = default);
    /// <summary>
    /// Used exclusively by LoginCommandHandler � bypasses global query filter
    /// because no JWT exists yet during authentication.
    /// </summary>
    Task<IReadOnlyList<UserRoleAssignment>> GetActiveByUserIgnoreFiltersAsync(
        Guid userId, IEnumerable<Guid> tenantIds, CancellationToken ct = default);
}
