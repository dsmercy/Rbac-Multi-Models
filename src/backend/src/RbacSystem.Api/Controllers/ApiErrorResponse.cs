namespace RbacSystem.Api.Controllers;

/// <summary>
/// Standard error envelope returned by <c>GlobalExceptionMiddleware</c> for all
/// non-2xx responses. The <c>code</c> field is a machine-readable constant
/// (e.g. <c>VALIDATION_ERROR</c>, <c>NOT_FOUND</c>, <c>TOKEN_STALE</c>).
/// </summary>
public sealed record ApiErrorResponse(
    /// <summary>Machine-readable error code.</summary>
    string Code,
    /// <summary>Human-readable description of the error.</summary>
    string Message,
    /// <summary>Correlation ID matching the <c>X-Request-Id</c> / ASP.NET trace identifier.</summary>
    string TraceId);
