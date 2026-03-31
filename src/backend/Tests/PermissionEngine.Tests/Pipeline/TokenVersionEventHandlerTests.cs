using FluentAssertions;
using NSubstitute;
using BuildingBlocks.Domain.Events;
using PermissionEngine.Application.EventHandlers;
using PermissionEngine.Domain.Interfaces;
using Xunit;

namespace PermissionEngine.Tests.Pipeline;

/// <summary>
/// Unit tests for the four Phase 4 token-version event handlers.
///
/// Spec: "any UserRoleAssigned, UserRoleRevoked, DelegationCreated, DelegationRevoked
/// event must atomically increment the user's token version in Redis."
///
/// Covers:
///   EH-01  UserRoleAssigned → IncrementTokenVersion for assigned user
///   EH-02  UserRoleRevoked  → InvalidateUser (increment + bust cache)
///   EH-03  DelegationCreated → IncrementTokenVersion for DELEGATEE (not delegator)
///   EH-04  DelegationRevoked → InvalidateUser for DELEGATEE
///   EH-05  DelegationCreated does NOT touch delegator's token version
///   EH-06  DelegationRevoked does NOT touch delegator's token version
/// </summary>
public sealed class TokenVersionEventHandlerTests
{
    private readonly IPermissionCacheService _cache =
        Substitute.For<IPermissionCacheService>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId   = Guid.NewGuid();
    private readonly Guid _roleId   = Guid.NewGuid();
    private readonly Guid _delegatorId  = Guid.NewGuid();
    private readonly Guid _delegateeId  = Guid.NewGuid();

    // EH-01 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task UserRoleAssigned_IncrementsAssignedUserTokenVersion()
    {
        var handler = new UserRoleAssignedTokenVersionHandler(_cache);
        var evt = new UserRoleAssignedEvent(
            Guid.NewGuid(), _tenantId, _userId, _roleId,
            scopeId: null, expiresAt: null, assignedByUserId: Guid.NewGuid());

        await handler.Handle(evt, default);

        await _cache.Received(1).IncrementTokenVersionAsync(_userId, default);
    }

    // EH-02 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task UserRoleRevoked_InvalidatesUserCache()
    {
        var handler = new UserRoleRevokedTokenVersionHandler(_cache);
        var evt = new UserRoleRevokedEvent(
            Guid.NewGuid(), _tenantId, _userId, _roleId,
            scopeId: null, revokedByUserId: Guid.NewGuid());

        await handler.Handle(evt, default);

        // InvalidateUser = increment version AND bust cached perm entries
        await _cache.Received(1).InvalidateUserAsync(_userId, _tenantId, default);
    }

    // EH-03 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task DelegationCreated_IncrementsOnlyDelegateeTokenVersion()
    {
        var handler = new DelegationCreatedTokenVersionHandler(_cache);
        var evt = new DelegationCreatedEvent(
            Guid.NewGuid(), _tenantId, _delegatorId, _delegateeId,
            permissionCodes: ["users:read"],
            scopeId: Guid.NewGuid(),
            expiresAt: DateTimeOffset.UtcNow.AddDays(1),
            chainDepth: 1);

        await handler.Handle(evt, default);

        await _cache.Received(1).IncrementTokenVersionAsync(_delegateeId, default);
    }

    // EH-04 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task DelegationRevoked_InvalidatesOnlyDelegateeCache()
    {
        var handler = new DelegationRevokedTokenVersionHandler(_cache);
        var evt = new DelegationRevokedEvent(
            Guid.NewGuid(), _tenantId, _delegateeId, revokedByUserId: Guid.NewGuid());

        await handler.Handle(evt, default);

        await _cache.Received(1).InvalidateUserAsync(_delegateeId, _tenantId, default);
    }

    // EH-05 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task DelegationCreated_DoesNotTouchDelegatorTokenVersion()
    {
        var handler = new DelegationCreatedTokenVersionHandler(_cache);
        var evt = new DelegationCreatedEvent(
            Guid.NewGuid(), _tenantId, _delegatorId, _delegateeId,
            permissionCodes: ["roles:read"],
            scopeId: Guid.NewGuid(),
            expiresAt: DateTimeOffset.UtcNow.AddDays(1),
            chainDepth: 1);

        await handler.Handle(evt, default);

        await _cache.DidNotReceive().IncrementTokenVersionAsync(_delegatorId, default);
        await _cache.DidNotReceive().InvalidateUserAsync(_delegatorId,
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // EH-06 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task DelegationRevoked_DoesNotTouchDelegatorTokenVersion()
    {
        var handler = new DelegationRevokedTokenVersionHandler(_cache);
        var evt = new DelegationRevokedEvent(
            Guid.NewGuid(), _tenantId, _delegateeId, revokedByUserId: _delegatorId);

        await handler.Handle(evt, default);

        await _cache.DidNotReceive().IncrementTokenVersionAsync(_delegatorId, default);
        await _cache.DidNotReceive().InvalidateUserAsync(_delegatorId,
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
