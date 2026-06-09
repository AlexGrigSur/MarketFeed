namespace MarketFeed.Abstractions;

public interface IProcessorMetrics
{
    /// <summary>
    /// Quotes actually inserted into storage (after de-duplication)
    /// </summary>
    void TicksPersisted(int count);

    /// <summary>
    /// A batch was persisted successfully
    /// </summary>
    void BatchSaved();

    /// <summary>
    /// A batch was dropped after a permanent (non-transient) failure
    /// </summary>
    void BatchDropped();

    /// <summary>
    /// The current number of quotes buffered in the channel
    /// </summary>
    void SetChannelDepth(int depth);

    /// <summary>
    /// How long persisting one batch took
    /// </summary>
    void RecordSaveDuration(TimeSpan elapsed);

    /// <summary>
    /// How many quotes were in a flushed batch
    /// </summary>
    void RecordBatchSize(int size);
}