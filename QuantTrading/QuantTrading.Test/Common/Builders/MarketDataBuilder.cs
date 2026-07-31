using QuantTrading.Shared.Models;

namespace QuantTrading.Test.Common.Builders;

public static class MarketDataBuilder
{
    private static readonly DateTime BaseDate =
        new(2024, 1, 1);

    /// Flat warm-up bars: Open=High=Low=Close=price, strictly increasing daily timestamps.
    public static List<MarketData> FlatBars(
        string symbol,
        int count,
        decimal price = 100m,
        int startDayOffset = 0)
    {
        List<MarketData> bars = new();
        for (int i = 0; i < count; i++)
        {
            bars.Add(Bar(
                symbol,
                BaseDate.AddDays(startDayOffset + i),
                open: price,
                close: price));
        }
        return bars;
    }

    public static MarketData Bar(
        string symbol,
        DateTime timestamp,
        decimal open,
        decimal close,
        decimal? high = null,
        decimal? low = null,
        decimal volume = 1000m)
    {
        return new MarketData(
            Symbol: symbol,
            Timestamp: timestamp,
            Open: open,
            High: high ?? Math.Max(open, close),
            Low: low ?? Math.Min(open, close),
            Close: close,
            Volume: volume);
    }
}
