using MarketFeed.Abstractions;
using Prometheus;

namespace MarketFeed.Host;

public sealed class PrometheusMetrics : IClientMetrics, IProcessorMetrics
{
    private static readonly string[] ExchangeLabel = ["exchange"];

    private readonly Counter _ticksReceived;
    private readonly Counter _ticksPersisted;
    private readonly Counter _reconnects;
    private readonly Counter _batchesSaved;
    private readonly Counter _batchesDropped;
    private readonly Counter _parseErrors;
    private readonly Gauge _channelDepth;
    private readonly Gauge _connectedClients;
    private readonly Histogram _batchSaveDuration;
    private readonly Histogram _batchSize;

    public PrometheusMetrics()
    {
        _ticksReceived = Metrics.CreateCounter("ticks_received_total",
            "Quotes parsed from exchanges and queued for processing.",
            new CounterConfiguration { LabelNames = ExchangeLabel });

        _ticksPersisted = Metrics.CreateCounter("ticks_persisted_total",
            "Quotes actually inserted into storage (after de-duplication).");

        _reconnects = Metrics.CreateCounter("reconnects_total",
            "Connections re-established after a dropped connection.",
            new CounterConfiguration { LabelNames = ExchangeLabel });

        _batchesSaved = Metrics.CreateCounter("batches_saved_total",
            "Batches persisted successfully.");

        _batchesDropped = Metrics.CreateCounter("batches_dropped_total",
            "Batches dropped after a permanent failure.");

        _parseErrors = Metrics.CreateCounter("parse_errors_total",
            "Messages that arrived but could not be parsed.",
            new CounterConfiguration { LabelNames = ExchangeLabel });

        _channelDepth = Metrics.CreateGauge("channel_depth",
            "Quotes currently buffered in the channel.");

        _connectedClients = Metrics.CreateGauge("connected_clients",
            "Client connection state per exchange (1 = connected, 0 = disconnected).",
            new GaugeConfiguration { LabelNames = ExchangeLabel });

        _batchSaveDuration = Metrics.CreateHistogram("batch_save_duration_seconds",
            "Time taken to persist one batch.");

        _batchSize = Metrics.CreateHistogram("batch_size",
            "Number of quotes in a flushed batch.",
            new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(1, 2, 14) });
    }

    public void TickReceived(string exchangeName) => _ticksReceived.WithLabels(exchangeName).Inc();
    public void Reconnected(string exchangeName) => _reconnects.WithLabels(exchangeName).Inc();
    public void ParseError(string exchangeName) => _parseErrors.WithLabels(exchangeName).Inc();
    public void SetConnected(string exchangeName, bool connected)
        => _connectedClients.WithLabels(exchangeName).Set(connected ? 1 : 0);

    public void TicksPersisted(int count) => _ticksPersisted.Inc(count);
    public void BatchSaved() => _batchesSaved.Inc();
    public void BatchDropped() => _batchesDropped.Inc();
    public void SetChannelDepth(int depth) => _channelDepth.Set(depth);
    public void RecordSaveDuration(TimeSpan elapsed) => _batchSaveDuration.Observe(elapsed.TotalSeconds);
    public void RecordBatchSize(int size) => _batchSize.Observe(size);
}