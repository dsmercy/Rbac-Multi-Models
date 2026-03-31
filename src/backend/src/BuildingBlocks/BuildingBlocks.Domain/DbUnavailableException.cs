namespace BuildingBlocks.Domain;

/// <summary>
/// Thrown when the database is unreachable during an operation.
/// Maps to HTTP 503 Service Unavailable — callers must distinguish
/// infrastructure failure (503) from permission denial (403).
/// </summary>
public sealed class DbUnavailableException : Exception
{
    public DbUnavailableException(string message) : base(message) { }

    public DbUnavailableException(string message, Exception inner)
        : base(message, inner) { }
}
