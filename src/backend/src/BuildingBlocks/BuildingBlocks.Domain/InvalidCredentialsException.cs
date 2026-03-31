namespace BuildingBlocks.Domain;

/// <summary>
/// Thrown when authentication fails (wrong password, unknown user, inactive account).
/// Maps to HTTP 401 Unauthorized — distinct from UnauthorizedAccessException
/// which maps to 403 Forbidden (authenticated but not allowed).
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message) : base(message) { }
}
