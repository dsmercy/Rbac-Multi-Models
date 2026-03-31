using BuildingBlocks.Application;
using Identity.Domain.Interfaces;
using MediatR;

namespace Identity.Application.EventHandlers;

/// <summary>
/// Revokes all active refresh tokens for a user when a stale JWT is detected.
///
/// Per spec: "Stale tv claim → 401 — invalidate refresh token, force re-login"
///
/// A stale token means a role or delegation changed after the JWT was issued.
/// Rather than allowing a silent refresh (which would issue a new access token
/// with updated permissions), we revoke the refresh tokens so the user must
/// re-authenticate with credentials. This closes the window where a user whose
/// role was revoked could silently obtain a new valid access token.
/// </summary>
public sealed class StaleTokenDetectedHandler
    : INotificationHandler<StaleTokenDetectedNotification>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public StaleTokenDetectedHandler(IRefreshTokenRepository refreshTokenRepository)
        => _refreshTokenRepository = refreshTokenRepository;

    public async Task Handle(
        StaleTokenDetectedNotification notification,
        CancellationToken cancellationToken)
    {
        var activeTokens = await _refreshTokenRepository.GetActiveByUserIdAsync(
            notification.UserId,
            notification.TenantId,
            cancellationToken);

        foreach (var token in activeTokens)
            token.Revoke("StaleTokenDetected");

        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
    }
}
