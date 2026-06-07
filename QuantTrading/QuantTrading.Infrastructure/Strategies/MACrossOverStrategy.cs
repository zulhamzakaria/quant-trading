using QuantTrading.Application.Interfaces;
using QuantTrading.Domain.Common;
using QuantTrading.Domain.Models;

namespace QuantTrading.Infrastructure.Strategies;

public sealed class MACrossOverStrategy : ISignalStrategy
{
    private readonly string _symbol;
    private readonly int _shortWindow, _longWindow;
    private readonly Queue<decimal> _prices = new();

    private decimal? _previousShortMA;
    private decimal? _previousLongMA;
    public MACrossOverStrategy(string symbol, 
        int shortWindow = 20, int longWindow = 50)
    {

        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException(
                "Symbol is required.",
                nameof(symbol));

        if (shortWindow <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(shortWindow),
                "Short window must be greater than zero.");

        if (longWindow <= shortWindow)
            throw new ArgumentOutOfRangeException(
                nameof(longWindow),
                "Long window must be greater than short window.");

        _symbol = symbol;
        _shortWindow = shortWindow;
        _longWindow = longWindow;
    }
    public Signal Update(MarketData data)
    {
        if (!string.Equals(
        data.Symbol,
        _symbol,
        StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Strategy configured for {_symbol} " +
                $"cannot process {data.Symbol}.");
        }

        _prices.Enqueue(data.Close);

        while (_prices.Count > _longWindow)
        {
            _prices.Dequeue();
        }

        if (_prices.Count < _longWindow)
        {
            return new Signal(
                data.Symbol,
                SignalType.Hold,
                0m,
                data.Timestamp,
                "MA Crossover");
        }

        var currentShortMa =
            _prices.TakeLast(_shortWindow).Average();

        var currentLongMa =
            _prices.Average();

        Signal signal = new(
            data.Symbol,
            SignalType.Hold,
            0m,
            data.Timestamp,
            "MA Crossover");

        if (_previousShortMA.HasValue &&
            _previousLongMA.HasValue)
        {
            var bullishCross =
                _previousShortMA <= _previousLongMA &&
                currentShortMa > currentLongMa;

            var bearishCross =
                _previousShortMA >= _previousLongMA &&
                currentShortMa < currentLongMa;

            if (bullishCross)
            {
                signal = new Signal(
                    data.Symbol,
                    SignalType.Buy,
                    0.5m,
                    data.Timestamp,
                    "MA Bullish Crossover");
            }
            else if (bearishCross)
            {
                signal = new Signal(
                    data.Symbol,
                    SignalType.Sell,
                    0.5m,
                    data.Timestamp,
                    "MA Bearish Crossover");
            }
        }

        _previousShortMA = currentShortMa;
        _previousLongMA = currentLongMa;

        return signal;
    }
}
