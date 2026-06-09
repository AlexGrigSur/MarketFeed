using MarketFeed.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace MarketFeed.StockExchangeClients.StockExchangeClientB;

[XmlRoot("tick")]
public sealed class ExchangeBStockQuote : IStockQuote
{
    private const string Exchnage = "ExchangeB";

    private string? _quoteId;

    [XmlIgnore]
    public string ExchangeName => Exchnage;

    [XmlElement("symbol")]
    public string Ticker { get; set; } = string.Empty;

    [XmlElement("last")]
    public decimal Price { get; set; }

    [XmlElement("vol")]
    public long Volume { get; set; }

    [XmlElement("time")]
    public DateTime WireTime { get; set; }

    [XmlIgnore]
    public DateTime Timestamp => WireTime.ToUniversalTime();

    [XmlIgnore]
    public string QuoteId => _quoteId ??= GenerateQuoteId();

    private string GenerateQuoteId()
    {
        var key = FormattableString.Invariant($"{ExchangeName}|{Ticker}|{Price}|{Volume}|{Timestamp.Ticks}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash, 0, 12);
    }
}
