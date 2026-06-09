using System.Net.WebSockets;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var address = builder.Configuration["ServerAddress"] ?? "http://localhost:5008";
var options = new MockServerOptions(
    builder.Configuration.GetValue("QuotesPerSecond", 50),
    builder.Configuration.GetValue("DuplicatePercent", 0),
    builder.Configuration.GetValue("DropConnectionMinSeconds", 0),
    builder.Configuration.GetValue("DropConnectionMaxSeconds", 0));
app.Urls.Add(address);

app.UseWebSockets();

app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var tickers = await ReceiveSubscriptionAsync(socket, context.RequestAborted);
    await StreamQuotesAsync(socket, tickers, options, context.RequestAborted);
});

app.Run();

static async Task<string[]> ReceiveSubscriptionAsync(WebSocket socket, CancellationToken ct)
{
    var buffer = new byte[4096];
    var result = await socket.ReceiveAsync(buffer, ct);
    if (result.MessageType == WebSocketMessageType.Close)
    {
        return [];
    }

    using var doc = JsonDocument.Parse(buffer.AsMemory(0, result.Count));

    var tickers = new List<string>();
    if (doc.RootElement.TryGetProperty("tickers", out var arr) && arr.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in arr.EnumerateArray())
        {
            if (item.GetString() is { Length: > 0 } ticker)
            {
                tickers.Add(ticker);
            }
        }
    }

    return tickers.ToArray();
}

static async Task StreamQuotesAsync(WebSocket socket, string[] requestedTickers, MockServerOptions options, CancellationToken ct)
{
    string[] tickers = requestedTickers.Length > 0
        ? requestedTickers
        : ["AAPL", "MSFT", "GOOG", "AMZN", "TSLA"];

    // Per-message Task.Delay can't pace high rates: 1/QuotesPerSecond seconds truncates to 0 ms
    // once the rate exceeds 1000, removing the throttle entirely. Instead send a burst per fixed tick.
    const int ticksPerSecond = 100;
    var tick = TimeSpan.FromSeconds(1.0 / ticksPerSecond);
    var perTick = Math.Max(1, options.QuotesPerSecond / ticksPerSecond);
    var dropAt = options.NextDropDeadline();
    var seq = 0L;
    byte[]? lastPayload = null;

    try
    {
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            if (dropAt is { } deadline && DateTime.UtcNow >= deadline)
            {
                socket.Abort(); // simulate a connection break; the client reconnects
                return;
            }

            for (var i = 0; i < perTick && socket.State == WebSocketState.Open; i++)
            {
                byte[] payload;
                if (lastPayload is not null && Random.Shared.Next(100) < options.DuplicatePercent)
                {
                    payload = lastPayload; // resend the exact same quote to exercise dedup
                }
                else
                {
                    payload = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        id = $"A-{Guid.NewGuid():N}",
                        sym = tickers[Random.Shared.Next(tickers.Length)],
                        px = Math.Round(100 + Random.Shared.NextDouble() * 50, 2),
                        qty = Random.Shared.Next(1, 1000),
                        t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        venue = "ExchangeA",
                        seq = seq++,
                        src = "mock"
                    });
                    lastPayload = payload;
                }

                await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, ct);
            }

            await Task.Delay(tick, ct);
        }
    }
    catch (OperationCanceledException) { }
    catch (WebSocketException) { }
}

sealed record MockServerOptions(int QuotesPerSecond, int DuplicatePercent, int DropMinSeconds, int DropMaxSeconds)
{
    public DateTime? NextDropDeadline()
        => DropMinSeconds > 0 && DropMaxSeconds >= DropMinSeconds
            ? DateTime.UtcNow.AddSeconds(Random.Shared.Next(DropMinSeconds, DropMaxSeconds + 1))
            : null;
}