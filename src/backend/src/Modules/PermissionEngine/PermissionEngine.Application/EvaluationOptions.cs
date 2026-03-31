namespace PermissionEngine.Application;

/// <summary>
/// Application-layer configuration for the permission evaluation service.
/// Bound from the <c>PermissionCache</c> section in appsettings.json.
/// </summary>
public sealed class EvaluationOptions
{
    public const string SectionName = "PermissionCache";

    /// <summary>
    /// Maximum wall-clock time (ms) allowed for the full evaluation pipeline.
    /// Requests that exceed this budget are denied rather than blocked.
    /// Default: 200 ms (per spec).
    /// </summary>
    public int EvaluationTimeoutMs { get; set; } = 200;
}
