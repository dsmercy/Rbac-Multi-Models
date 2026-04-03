using Microsoft.EntityFrameworkCore;
using RbacCore.Domain.Entities;
using RbacCore.Domain.Interfaces;

namespace RbacCore.Infrastructure.Persistence.Repositories;

public sealed class UserRoleAssignmentRepository : IUserRoleAssignmentRepository
{
    private readonly RbacDbContext _context;

    public UserRoleAssignmentRepository(RbacDbContext context) => _context = context;

    public async Task<IReadOnlyList<UserRoleAssignment>> GetActiveByUserAsync(
        Guid userId, Guid tenantId, Guid? scopeId, CancellationToken ct = default)
        => await _context.UserRoleAssignments
            .Where(a => a.UserId == userId &&
                        a.TenantId == tenantId &&
                        a.ScopeId == scopeId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserRoleAssignment>> GetAllActiveByUserAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
        => await _context.UserRoleAssignments
            .Where(a => a.UserId == userId && a.TenantId == tenantId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserRoleAssignment>> GetActiveByRoleAsync(
        Guid roleId, Guid tenantId, CancellationToken ct = default)
    {
        // Use IgnoreQueryFilters to find ALL active assignments regardless of expiry
        // (cascade deactivation must deactivate even if filter would exclude them)
        return await _context.UserRoleAssignments
            .IgnoreQueryFilters()
            .Where(a => a.RoleId == roleId &&
                        a.TenantId == tenantId &&
                        a.IsActive)
            .ToListAsync(ct);
    }

    public Task<UserRoleAssignment?> GetByIdAsync(Guid assignmentId, CancellationToken ct = default)
        => _context.UserRoleAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);

    public Task<bool> AssignmentExistsAsync(
        Guid userId, Guid roleId, Guid tenantId, Guid? scopeId, CancellationToken ct = default)
        => _context.UserRoleAssignments
            .AnyAsync(a => a.UserId == userId &&
                           a.RoleId == roleId &&
                           a.TenantId == tenantId &&
                           a.ScopeId == scopeId, ct);

    public async Task AddAsync(UserRoleAssignment assignment, CancellationToken ct = default)
        => await _context.UserRoleAssignments.AddAsync(assignment, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
    public async Task<IReadOnlyList<UserRoleAssignment>> GetActiveByUserIgnoreFiltersAsync(
    Guid userId, IEnumerable<Guid> tenantIds, CancellationToken ct = default)
    {
        var ids = tenantIds.ToList();
        return await _context.UserRoleAssignments
            .IgnoreQueryFilters()                          // no JWT during login
            .Where(a =>
                a.UserId == userId &&
                ids.Contains(a.TenantId) &&
                a.IsActive &&
                (a.ExpiresAt == null || a.ExpiresAt > DateTimeOffset.UtcNow) &&
                !a.IsDeleted)
            .ToListAsync(ct);
    }
}
