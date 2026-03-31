namespace PermissionEngine.Domain.Exceptions;

/// <summary>
/// Thrown by TokenVersionValidationStep when the JWT "tv" claim does not
/// match the current Redis token-version:{userId} value.
/// Mapped to HTTP 401 by GlobalExceptionMiddleware so clients can distinguish
/// "re-authenticate" (401) from "no permission" (403).
/// </summary>
public sealed class StaleTokenException : Exception
{
    public StaleTokenException(string message) : base(message) { }
}