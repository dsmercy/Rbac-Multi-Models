using BuildingBlocks.Domain.Events;
using FluentAssertions;
using NSubstitute;
using PermissionEngine.Application.EventHandlers;
using PermissionEngine.Domain.Interfaces;
using Xunit;

namespace PermissionEngine.Tests.Pipeline;

/// <summary>
/// Unit tests for the Phase 5 policy and tenant cache eviction event handlers.
///
/// Spec (CLAUDE.md eviction map):
///   PolicyCreated / PolicyUpdated / PolicyDeleted
///     → InvalidateTenantPermCacheAsync(tenantId)
///   TenantSuspended
///     → InvalidateAllTenantKeysAsync(tenantId)
///
/// Covers:
///   PCE-01  PolicyCreated  → InvalidateTenantPermCache called with correct tenant
///   PCE-02  PolicyUpdated  → InvalidateTenantPermCache called with correct tenant
///   PCE-03  PolicyDeleted  → InvalidateTenantPermCache called with correct tenant
///   PCE-04  TenantSuspended → InvalidateAllTenantKeys called with correct tenant
///   PCE-05  PolicyCreated on tenant A does NOT bust tenant B's cache
/// </summary>
public sealed class PolicyCacheEvictionHandlerTests
{
    private readonly IPermissionCacheService _cache =
        Substitute.For<IPermissionCacheService>();

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    // PCE-01 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task PolicyCreated_InvalidatesTenantPermCache()
    {
        var handler = new PolicyCacheEvictionHandler(_cache);
        var evt = new PolicyCreatedEvent(Guid.NewGuid(), _tenantA, "TestPolicy", "Allow", Guid.NewGuid());

        await handler.Handle(evt, default);

        await _cache.Received(1).InvalidateTenantPermCacheAsync(_tenantA, default);
    }

    // PCE-02 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task PolicyUpdated_InvalidatesTenantPermCache()
    {
        var handler = new PolicyCacheEvictionHandler(_cache);
        var evt = new PolicyUpdatedEvent(Guid.NewGuid(), _tenantA, Guid.NewGuid());

        await handler.Handle(evt, default);

        await _cache.Received(1).InvalidateTenantPermCacheAsync(_tenantA, default);
    }

    // PCE-03 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task PolicyDeleted_InvalidatesTenantPermCache()
    {
        var handler = new PolicyCacheEvictionHandler(_cache);
        var evt = new PolicyDeletedEvent(Guid.NewGuid(), _tenantA, Guid.NewGuid());

        await handler.Handle(evt, default);

        await _cache.Received(1).InvalidateTenantPermCacheAsync(_tenantA, default);
    }

    // PCE-04 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TenantSuspended_InvalidatesAllTenantKeys()
    {
        var handler = new TenantSuspendedCacheEvictionHandler(_cache);
        var evt = new TenantSuspendedEvent(_tenantA, "Compliance violation", Guid.NewGuid());

        await handler.Handle(evt, default);

        await _cache.Received(1).InvalidateAllTenantKeysAsync(_tenantA, default);
    }

    // PCE-05 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task PolicyCreated_ForTenantA_DoesNotBustTenantB()
    {
        var handler = new PolicyCacheEvictionHandler(_cache);
        var evt = new PolicyCreatedEvent(Guid.NewGuid(), _tenantA, "TestPolicy", "Deny", Guid.NewGuid());

        await handler.Handle(evt, default);

        await _cache.DidNotReceive().InvalidateTenantPermCacheAsync(_tenantB, Arg.Any<CancellationToken>());
    }
}
