using MarketFeed.Abstractions;
using MarketFeed.Host.Settings;
using Polly;
using Polly.Retry;
using System.Diagnostics;
using System.Threading.Channels;

namespace MarketFeed.Host;

public class StockExchangeProcessor : BackgroundService
{
    private readonly IReadOnlyList<IStockExchangeClient> _clients;
    private readonly IStockQuoteRepository _repository;
    private readonly IProcessorMetrics _metrics;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<StockExchangeProcessor> _logger;

    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;

    private readonly Channel<IStockQuote> _quoteChannel;
    private readonly ResiliencePipeline _savePipeline;

    private long _processedQuotes;

    public StockExchangeProcessor(
        IReadOnlyList<IStockExchangeClient> clients,
        IStockQuoteRepository repository,
        StockExchangeProcessorConfiguration options,
        IProcessorMetrics metrics,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<StockExchangeProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(hostApplicationLifetime);
        ArgumentNullException.ThrowIfNull(logger);

        _clients = clients;
        _repository = repository;
        _metrics = metrics;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;

        _batchSize = options.BatchSize;
        _flushInterval = options.FlushInterval;

        _quoteChannel = Channel.CreateBounded<IStockQuote>(options.ChannelMaxSize);

        _savePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<TransientStorageException>(),
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
                MaxDelay = TimeSpan.FromSeconds(5),
                OnRetry = args =>
                {
                    _logger.LogWarning(args.Outcome.Exception,
                        "Saving quotes failed (attempt {Attempt}), retrying in {Delay}",
                        args.AttemptNumber, args.RetryDelay);
                    return default;
                }
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting stock exchange processor with {ClientCount} clients", _clients.Count);

        try
        {
            await StartClientsAsync(stoppingToken);
            await ProcessChannelAsync(stoppingToken);
        }
        finally
        {
            _logger.LogInformation("Stock exchange processor is stopping. Processed {ProcessedQuotes} quotes in total.", _processedQuotes);
        }
    }

    private async Task StartClientsAsync(CancellationToken cancellationToken)
    {
        foreach (var client in _clients)
        {
            try
            {
                await client.StartAsync(_quoteChannel.Writer, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Cannot start {Exchange} client. Exception: ", client.InstanceName);
                _hostApplicationLifetime.StopApplication();
                return;
            }
        }
    }

    private async Task ProcessChannelAsync(CancellationToken cancellationToken)
    {
        var reader = _quoteChannel.Reader;
        var buffer = new IStockQuote[_batchSize];
        var count = 0;

        var delayTask = Task.Delay(_flushInterval, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && 
                        count != _batchSize &&
                        !delayTask.IsCompleted &&
                        reader.TryRead(out var quote))
                {
                    buffer[count++] = quote;
                }

                _metrics.SetChannelDepth(reader.Count);

                if (count == _batchSize || delayTask.IsCompleted)
                {
                    if (count > 0)
                    {
                        await SaveBatchAsync(buffer, count, cancellationToken);
                        Array.Clear(buffer, 0, count);
                        count = 0;
                    }

                    delayTask = Task.Delay(_flushInterval, cancellationToken);
                    continue;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing quote channel. Exception: ");
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        // flushing remaining quotes before shutdown
        _quoteChannel.Writer.TryComplete();

        if (count > 0)
        {
            await SaveBatchAsync(buffer, count, CancellationToken.None);
        }

        if (reader.Count > 0)
        {
            var remainingQuotes = await reader.ReadAllAsync(CancellationToken.None).ToArrayAsync(CancellationToken.None);
            await SaveBatchAsync(remainingQuotes, remainingQuotes.Length, CancellationToken.None);
        }
    }

    private async Task SaveBatchAsync(IStockQuote[] buffer, int count, CancellationToken cancellationToken)
    {
        IReadOnlyList<IStockQuote> batch = count == _batchSize
            ? buffer
            : new ArraySegment<IStockQuote>(buffer, 0, count);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var inserted = await _savePipeline.ExecuteAsync(
                static async (state, ct) => await state._repository.SaveAsync(state.batch, ct),
                (_repository, batch),
                cancellationToken);
            stopwatch.Stop();

            _processedQuotes += count;
            _metrics.BatchSaved();
            _metrics.TicksPersisted(inserted);
            _metrics.RecordBatchSize(count);
            _metrics.RecordSaveDuration(stopwatch.Elapsed);
            _logger.LogDebug("Persisted batch: {Inserted} new of {Count} quotes (total processed: {Total})", inserted, count, _processedQuotes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _metrics.BatchDropped();
            _logger.LogError(ex, "Failed to persist batch of {Count} quotes after retries; batch dropped. Exception: ", count);
        }
    }
}