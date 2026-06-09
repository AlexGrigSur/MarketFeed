using CommunityToolkit.HighPerformance.Buffers;
using MarketFeed.Abstractions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Channels;

namespace MarketFeed.StockExchangeClients.Common;

public abstract class BaseStockClient : IStockExchangeClient
{
    private const int BufferSizeBytes = 8 * 1024; // 8 kbytes
    private const int MaxMessageSizeBytes = 1 * 1024 * 1024; // 1 megabyte
    private static readonly TimeSpan ReconnectionDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan WorkerStopTimeout = TimeSpan.FromSeconds(1);

    private readonly ILogger _logger;
    private readonly IClientMetrics _metrics;

    private readonly byte[] _buffer;
    private readonly ResiliencePipeline<ClientWebSocket> _connectPipeline;

    private bool _isStarted = false;
    private ChannelWriter<IStockQuote> _writer = null!;
    private CancellationTokenSource _cts = null!;
    private Task _workerTask = null!;

    protected Uri Endpoint { get; }
    protected TimeSpan IdleTimeout { get; }
    public string InstanceName { get; }

    protected BaseStockClient(BaseStockExchangeClientConfiguration configuration, ILogger logger, IClientMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(metrics);

        InstanceName = configuration.InstanceName;
        Endpoint = new Uri(configuration.Endpoint);
        IdleTimeout = configuration.IdleTimeout;
        _logger = logger;
        _metrics = metrics;

        _buffer = new byte[BufferSizeBytes];
        _connectPipeline = new ResiliencePipelineBuilder<ClientWebSocket>()
           .AddRetry(new RetryStrategyOptions<ClientWebSocket>
           {
               ShouldHandle = new PredicateBuilder<ClientWebSocket>()
                   .Handle<WebSocketException>()
                   .Handle<IOException>()
                   .Handle<SocketException>(),
               MaxRetryAttempts = configuration.MaxRetryAttempts,
               BackoffType = DelayBackoffType.Exponential,
               UseJitter = true,
               Delay = configuration.ReconnectDelay,
               MaxDelay = configuration.MaxReconnectDelay,
               OnRetry = args =>
               {
                   _logger.LogWarning("{Exchange}: connect failed (attempt {N}), retry in {Delay}",
                       InstanceName, args.AttemptNumber, args.RetryDelay);
                   return default;
               }
           })
           .Build();
    }

    public async Task StartAsync(ChannelWriter<IStockQuote> writer, CancellationToken cancellationToken)
    {
        if (_isStarted)
        {
            throw new InvalidOperationException("Client was already started");
        }

        ArgumentNullException.ThrowIfNull(writer);

        await StartInternalAsync(cancellationToken);

        _writer = writer;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _workerTask = Task.Factory
            .StartNew(ProcessAsync, _cts.Token, TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously)
            .Unwrap();

        _isStarted = true;
    }

    public void Dispose()
    {
        if (!_isStarted)
        {
            return;
        }

        _isStarted = false;

        _cts.Cancel();

        if (!_workerTask.Wait(WorkerStopTimeout))
        {
            _logger.LogWarning("{Exchange}: worker did not stop within {Timeout}", InstanceName, WorkerStopTimeout);
        }

        _cts.Dispose();
    }

    protected virtual Task StartInternalAsync(CancellationToken cancellationToken) { return Task.CompletedTask; }
    protected abstract ValueTask SubscribeAsync(ClientWebSocket ws, CancellationToken ct);
    protected abstract bool TryParse(ReadOnlySpan<byte> rawMessage, out IStockQuote stockQuote);
    protected virtual void ConfigureWebSocket(ClientWebSocketOptions options) { }

    private async Task ProcessAsync(object? data)
    {
        var cancellationToken = (CancellationToken)data!;
        var firstAttempt = true;

        while (!cancellationToken.IsCancellationRequested)
        {
            ClientWebSocket? ws = null;
            try
            {
                _logger.LogInformation("{Exchange}: {Action} to {Endpoint}",
                    InstanceName, firstAttempt ? "connecting" : "reconnecting", Endpoint);

                ws = await _connectPipeline.ExecuteAsync(static async (state, ct) =>
                {
                    var socket = state.CreateWebSocket();
                    try
                    {
                        await socket.ConnectAsync(state.Endpoint, ct);
                        return socket;
                    }
                    catch (Exception ex)
                    {
                        var status = socket.HttpStatusCode;
                        socket.Dispose();

                        if (IsPermanentHandshakeStatus(status))
                        {
                            throw new PermanentConnectionException(status, ex);
                        }

                        throw;
                    }
                }, this, cancellationToken);

                if (!firstAttempt)
                {
                    _metrics.Reconnected(InstanceName);
                }

                firstAttempt = false;
                _metrics.SetConnected(InstanceName, true);
                _logger.LogInformation("{Exchange}: connected to {Endpoint}", InstanceName, Endpoint);

                await SubscribeAsync(ws, cancellationToken);
                _logger.LogDebug("{Exchange}: subscribed", InstanceName);

                await ReceiveLoopAsync(ws, cancellationToken);
                _logger.LogInformation("{Exchange}: connection closed by remote", InstanceName);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (PermanentConnectionException ex)
            {
                _logger.LogCritical(ex, "{Exchange}: permanent connection failure, giving up", InstanceName);
                break;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("{Exchange}: idle timeout — no data for {IdleTimeout}, dropping connection", InstanceName, IdleTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Exchange}: connection lost", InstanceName);
            }
            finally
            {
                _metrics.SetConnected(InstanceName, false);
                ws?.Dispose();
            }

            await Task.Delay(ReconnectionDelay, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        _logger.LogInformation("{Exchange}: stopped", InstanceName);
    }


    private ClientWebSocket CreateWebSocket()
    {
        var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        ws.Options.CollectHttpResponseDetails = true;
        ConfigureWebSocket(ws.Options);
        return ws;
    }

    private static bool IsPermanentHandshakeStatus(HttpStatusCode status)
    {
        return
            (int)status is >= 400 and < 500 &&
            status is not (HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests);
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken stoppingToken)
    {
        using var idleCts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, idleCts.Token);
        idleCts.CancelAfter(IdleTimeout);

        while (!linked.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(_buffer.AsMemory(), linked.Token);
            idleCts.CancelAfter(IdleTimeout);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                return;
            }

            ArrayPoolBufferWriter<byte>? assembled = null;
            try
            {
                ReadOnlyMemory<byte> message;
                if (result.EndOfMessage) // single-frame message - using shared buffer directly
                {
                    message = _buffer.AsMemory(0, result.Count);
                }
                else // multi-frame message, needs assembly
                {
                    assembled = new ArrayPoolBufferWriter<byte>(_buffer.Length * 2);
                    await ReadFragmentedAsync(ws, result, assembled, linked.Token);
                    message = assembled.WrittenMemory;
                }

                if (TryParse(message.Span, out var quote))
                {
                    _metrics.TickReceived(InstanceName);
                    await _writer.WriteAsync(quote, stoppingToken);
                }
                else
                {
                    _metrics.ParseError(InstanceName);
                    _logger.LogDebug("{Exchange}: unparseable message skipped", InstanceName);
                }
            }
            finally
            {
                assembled?.Dispose();
            }
        }
    }

    private async Task ReadFragmentedAsync(
        ClientWebSocket client,
        ValueWebSocketReceiveResult firstFrame,
        ArrayPoolBufferWriter<byte> writer,
        CancellationToken ct)
    {
        writer.Write(_buffer.AsSpan(0, firstFrame.Count));

        var result = firstFrame;
        while (!result.EndOfMessage)
        {
            result = await client.ReceiveAsync(_buffer.AsMemory(), ct);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
            }

            if (writer.WrittenCount + result.Count > MaxMessageSizeBytes)
            {
                throw new InvalidOperationException($"Message exceeded {MaxMessageSizeBytes} bytes");
            }

            writer.Write(_buffer.AsSpan(0, result.Count));
        }
    }
}