using QuantTrading.Application.Interfaces;
using QuantTrading.Domain.Models;
using QuantTrading.Shared.Models;

namespace QuantTrading.Infrastructure.Strategies;

public sealed class BreakoutStrategy: ISignalStrategy
{
    private readonly string _symbol;
    private readonly int _lookbackPeriod;

    private readonly Queue<decimal> _highs = new();
    private readonly Queue<decimal> _lows = new();

    public BreakoutStrategy(string symbol, int lookbackPeriod = 20)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));

        if (lookbackPeriod < 2)
            throw new ArgumentException("Lookback period must be at least 2.", nameof(lookbackPeriod));

        _symbol = symbol;
        _lookbackPeriod = lookbackPeriod;
    }

    public Signal Update(MarketData data)
    {
        if (!string.Equals(data.Symbol, _symbol, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Strategy configured for {_symbol} cannot process {data.Symbol}.");
        }

        // 1. Check breakout against PREVIOUS history before enqueuing current bar
        if (_highs.Count == _lookbackPeriod)
        {
            // O(N) here is fine for small MVP windows, but it's now mathematically correct
            var highestHigh = _highs.Max();
            var lowestLow = _lows.Min();

            // Clear lookahead bias: current Close vs previous historical extremes
            if (data.Close > highestHigh)
            {
                UpdateHistory(data);
                return Signal.Buy(data.Symbol, data.Timestamp, 0.7m, "Breakout High");
            }

            if (data.Close < lowestLow)
            {
                UpdateHistory(data);
                return Signal.Sell(data.Symbol, data.Timestamp, 0.7m, "Breakout Low");
            }
        }

        // 2. Update sliding memory for the NEXT bar's evaluation
        UpdateHistory(data);

        return Signal.Hold(data.Symbol, data.Timestamp);
    }
    private void UpdateHistory(MarketData data)
    {
        _highs.Enqueue(data.High);
        _lows.Enqueue(data.Low);

        if (_highs.Count > _lookbackPeriod)
        {
            _highs.Dequeue();
            _lows.Dequeue();
        }
    }
}
