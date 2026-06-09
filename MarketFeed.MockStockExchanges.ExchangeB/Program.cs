using System.Net.WebSockets;
using System.Text;
using System.Xml.Linq;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var address = builder.Configuration["ServerAddress"] ?? "http://localhost:5073";
var authToken = builder.Configuration["AuthToken"] ?? "secret-token";
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

    string? authorization = context.Request.Headers.Authorization;
    if (authorization != $"Bearer {authToken}")
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
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

    var root = XElement.Parse(Encoding.UTF8.GetString(buffer, 0, result.Count));
    return root.Elements("symbol")
        .Select(e => e.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToArray();
}

static async Task StreamQuotesAsync(WebSocket socket, string[] requestedTickers, MockServerOptions options, CancellationToken ct)
{
    string[] tickers = requestedTickers.Length > 0
        ? requestedTickers
        : ["AAPL", "MSFT", "GOOG", "AMZN", "TSLA"];

    const int ticksPerSecond = 100;
    var tick = TimeSpan.FromSeconds(1.0 / ticksPerSecond);
    var perTick = Math.Max(1, options.QuotesPerSecond / ticksPerSecond);
    var dropAt = options.NextDropDeadline();
    byte[]? lastPayload = null;

    try
    {
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            if (dropAt is { } deadline && DateTime.UtcNow >= deadline)
            {
                socket.Abort();
            }

            for (var i = 0; i < perTick && socket.State == WebSocketState.Open; i++)
            {
                byte[] payload;
                if (lastPayload is not null && Random.Shared.Next(100) < options.DuplicatePercent)
                {
                    payload = lastPayload;
                }
                else
                {
                    var ticker = tickers[Random.Shared.Next(tickers.Length)];
                    var price = Math.Round(100 + Random.Shared.NextDouble() * 50, 2);
                    var volume = Random.Shared.Next(1, 1000);

                    var xml = FormattableString.Invariant(
                        $"<tick><symbol>{ticker}</symbol><last>{price}</last><vol>{volume}</vol><time>{DateTime.UtcNow:O}</time></tick>");
                    payload = Encoding.UTF8.GetBytes(xml);
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