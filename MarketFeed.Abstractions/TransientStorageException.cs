namespace MarketFeed.Abstractions;

/// <summary>
/// A storage failure that is safe to retry (connection drops, deadlocks, timeouts).
/// </summary>
public sealed class TransientStorageException : Exception
{
    public TransientStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}