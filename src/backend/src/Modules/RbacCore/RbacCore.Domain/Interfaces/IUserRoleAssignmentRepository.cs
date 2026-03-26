using RbacCore.Domain.Entities;

namespace RbacCore.Domain.Interfaces;

public interface IUserRoleAssignmentRepository
{
    Task<IReadOnlyList<UserRoleAssignment>> GetActiveByUserAsync(
        Guid userId, Guid tenantId, Guid? scopeId, CancellationToken ct = default);

    Task<IReadOnlyList<UserRoleAssignment>> GetActiveByRoleAsync(
        Guid roleId, Guid tenantId, CancellationToken ct = default);

    Task<UserRoleAssignment?> GetByIdAsync(
        Guid assignmentId, CancellationToken ct = default);

    Task<bool> AssignmentExistsAsync(
        Guid userId, Guid roleId, Guid tenantId, Guid? scopeId, CancellationToken ct = default);

    Task AddAsync(UserRoleAssignment assignment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
