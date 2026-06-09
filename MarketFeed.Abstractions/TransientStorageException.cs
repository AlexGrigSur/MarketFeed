namespace MarketFeed.Abstractions;

/// <summary>
/// A storage failure that is safe to retry (connection drops, deadlocks, timeouts).
/// Permanent faults (bad data, schema mismatch, …) are surfaced as their original type.
/// </summary>
public sealed class TransientStorageException : Exception
{
    public TransientStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
