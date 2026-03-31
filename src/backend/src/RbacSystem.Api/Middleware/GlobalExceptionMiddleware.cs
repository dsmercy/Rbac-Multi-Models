using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PermissionEngine.Domain.Exceptions;
using System.Data.Common;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RbacSystem.Api.Middleware;

/// <summary>
/// Central exception handler implementing Phase 4 failure-handling spec.
///
/// Failure mode mapping:
///   ValidationException          → 400 Bad Request      (VALIDATION_ERROR)
///   DomainException              → 422 Unprocessable    (domain code)
///   UnauthorizedAccessException  → 403 Forbidden        (FORBIDDEN)
///   SecurityTokenException       → 401 Unauthorized     (TOKEN_INVALID)
///   StaleTokenException          → 401 Unauthorized     (TOKEN_STALE)
///   KeyNotFoundException         → 404 Not Found        (NOT_FOUND)
///   InvalidOperationException    → 400 Bad Request      (INVALID_OPERATION)
///   TimeoutException             → 503 Service Unavail. (EVAL_TIMEOUT)
///   DbUnavailableException       → 503 Service Unavail. (SERVICE_UNAVAILABLE)
///   unhandled                    → 500 Internal Error   (INTERNAL_ERROR)
///
/// Security notes:
///   • Stack traces are NEVER included in responses (OWASP A05).
///   • Raw JWT values are NEVER logged.
///   • IP and User-Agent are logged on 401 events for threat detection.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, code, message) = MapException(exception);

        // Log 401 events with IP + UA for security monitoring.
        // For corrupt/malformed JWT: also log SHA256 hash of raw token (never the token itself).
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            if (exception is SecurityTokenException)
            {
                var tokenHash = HashBearerToken(context.Request.Headers.Authorization.ToString());
                _logger.LogWarning(
                    "Invalid token [{Code}] for {Method} {Path} | IP={Ip} UA={UA} TokenHash={TokenHash}",
                    code,
                    context.Request.Method,
                    context.Request.Path,
                    context.Connection.RemoteIpAddress,
                    context.Request.Headers.UserAgent.ToString(),
                    tokenHash);
            }
            else
            {
                _logger.LogWarning(
                    "Authentication failure [{Code}] for {Method} {Path} | IP={Ip} UA={UA}",
                    code,
                    context.Request.Method,
                    context.Request.Path,
                    context.Connection.RemoteIpAddress,
                    context.Request.Headers.UserAgent.ToString());
            }
        }
        else if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception,
                "Unhandled exception [{Code}] for {Method} {Path}",
                code,
                context.Request.Method,
                context.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                "Request error [{Code}] {Message} for {Method} {Path}",
                code, message,
                context.Request.Method,
                context.Request.Path);
        }

        // Gap 2 fix: stale token → revoke refresh tokens so re-login is forced.
        // Publish asynchronously; failure to revoke must not prevent the 401 response.
        if (exception is StaleTokenException)
            await TryPublishStaleTokenDetectedAsync(context);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        // Never include stack trace in response (OWASP A05)
        var body = JsonSerializer.Serialize(new
        {
            code,
            message,
            traceId = context.TraceIdentifier
        });

        await context.Response.WriteAsync(body);
    }

    private static (HttpStatusCode status, string code, string message) MapException(
        Exception exception) => exception switch
    {
        // ── 400 Bad Request ───────────────────────────────────────────────────
        ValidationException ve => (
            HttpStatusCode.BadRequest,
            "VALIDATION_ERROR",
            string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),

        InvalidOperationException ioe => (
            HttpStatusCode.BadRequest,
            "INVALID_OPERATION",
            ioe.Message),

        // ── 401 Unauthorized ──────────────────────────────────────────────────
        // Stale token version — role or delegation changed after token was issued.
        // Refresh tokens are revoked via StaleTokenDetectedNotification (MediatR).
        StaleTokenException => (
            HttpStatusCode.Unauthorized,
            "TOKEN_STALE",
            "Your session token is outdated due to a permission change. Please re-authenticate."),

        // Corrupt or malformed JWT — log IP + UA handled by the 401 log block above.
        SecurityTokenException => (
            HttpStatusCode.Unauthorized,
            "TOKEN_INVALID",
            "The provided token is invalid or malformed. Please re-authenticate."),

        // Authentication failure (wrong password, unknown user, account locked).
        // Distinct from UnauthorizedAccessException which is authorization (403).
        InvalidCredentialsException => (
            HttpStatusCode.Unauthorized,
            "INVALID_CREDENTIALS",
            "Invalid email or password."),

        // ── 403 Forbidden ─────────────────────────────────────────────────────
        UnauthorizedAccessException => (
            HttpStatusCode.Forbidden,
            "FORBIDDEN",
            "You do not have permission to perform this action."),

        // ── 404 Not Found ─────────────────────────────────────────────────────
        KeyNotFoundException => (
            HttpStatusCode.NotFound,
            "NOT_FOUND",
            "The requested resource was not found."),

        // ── 422 Unprocessable Entity ──────────────────────────────────────────
        DomainException de => (
            HttpStatusCode.UnprocessableEntity,
            de.Code,
            de.Message),

        // ── 503 Service Unavailable ───────────────────────────────────────────
        // Spec: "DB unavailable → 503, not 403 — caller must distinguish infra failure from denial"
        DbUnavailableException => (
            HttpStatusCode.ServiceUnavailable,
            "SERVICE_UNAVAILABLE",
            "A required service is temporarily unavailable. Please retry."),

        // Raw DB exceptions (NpgsqlException, SqlException) that bubble up without being
        // wrapped in DbUnavailableException — e.g. connection refused at startup.
        // DbException is the BCL base for all ADO.NET provider exceptions.
        DbException => (
            HttpStatusCode.ServiceUnavailable,
            "SERVICE_UNAVAILABLE",
            "A required service is temporarily unavailable. Please retry."),

        // EF Core wraps connection failures in DbUpdateException
        DbUpdateException => (
            HttpStatusCode.ServiceUnavailable,
            "SERVICE_UNAVAILABLE",
            "A required service is temporarily unavailable. Please retry."),

        TimeoutException => (
            HttpStatusCode.ServiceUnavailable,
            "EVAL_TIMEOUT",
            "Permission evaluation timed out. Please retry."),

        // ── 500 Internal Server Error ─────────────────────────────────────────
        _ => (
            HttpStatusCode.InternalServerError,
            "INTERNAL_ERROR",
            "An unexpected error occurred.")
    };

    /// <summary>
    /// Computes SHA256 of the raw bearer token for security logging.
    /// Extracts "Bearer &lt;token&gt;" from the Authorization header value.
    /// Never logs the raw token — only its hash (OWASP A09).
    /// Returns "none" when no bearer token is present.
    /// </summary>
    private static string HashBearerToken(string authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return "none";

        var rawToken = authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader["Bearer ".Length..]
            : authorizationHeader;

        if (string.IsNullOrWhiteSpace(rawToken))
            return "none";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Publishes <see cref="StaleTokenDetectedNotification"/> so Identity module can
    /// revoke all active refresh tokens, forcing a full re-login.
    /// Extracts userId and tenantId from the already-authenticated JWT claims.
    /// Swallows exceptions — revocation failure must not mask the 401 response.
    /// </summary>
    private async Task TryPublishStaleTokenDetectedAsync(HttpContext context)
    {
        try
        {
            var subClaim = context.User.FindFirst("sub")?.Value
                        ?? context.User.FindFirst(
                               System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var tidClaim = context.User.FindFirst("tid")?.Value;

            if (!Guid.TryParse(subClaim, out var userId) ||
                !Guid.TryParse(tidClaim, out var tenantId))
                return; // Claims unavailable (e.g. unauthenticated request) — skip

            var publisher = context.RequestServices.GetService<IPublisher>();
            if (publisher is null) return;

            await publisher.Publish(
                new StaleTokenDetectedNotification(userId, tenantId),
                context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish StaleTokenDetectedNotification — refresh tokens may not be revoked");
        }
    }
}
