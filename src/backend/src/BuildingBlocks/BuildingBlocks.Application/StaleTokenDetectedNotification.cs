using MediatR;

namespace BuildingBlocks.Application;

/// <summary>
/// Published by GlobalExceptionMiddleware when TokenVersionValidationStep
/// detects a stale JWT "tv" claim (role or delegation changed after token was issued).
///
/// Identity.Application handles this to revoke all active refresh tokens for the
/// user, forcing a full re-login rather than a silent token refresh.
///
/// Per spec: "Stale tv claim → 401 — invalidate refresh token, force re-login"
/// </summary>
public sealed record StaleTokenDetectedNotification(
    Guid UserId,
    Guid TenantId) : INotification;
