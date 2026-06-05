using QuantTrading.Application.Interfaces;
using QuantTrading.Application.Models;
using QuantTrading.Domain.Models;

namespace QuantTrading.Infrastructure.Strategies;

public sealed class RSIStrategy : ISignalStrategy
{
    private readonly string _symbol;
    private readonly int _period;
    private readonly decimal _oversoldThreshold;
    private readonly decimal _overboughtThreshold;

    private readonly Queue<decimal> _changes = new();
    private decimal? _previousClose;

    // Running sums for O(1) incremental tracking
    private decimal _runningGainsSum;
    private decimal _runningLossesSum;
    private bool _isBaselineEstablished;

    public RSIStrategy(
        string symbol,
        int period = 14,
        decimal oversoldThreshold = 30m,
        decimal overboughtThreshold = 70m)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        if (period < 2)
            throw new ArgumentException("Period must be at least 2.", nameof(period));
        if (oversoldThreshold <= 0 || oversoldThreshold >= 100)
            throw new ArgumentException("Oversold threshold must be between 0 and 100.", nameof(oversoldThreshold));
        if (overboughtThreshold <= 0 || overboughtThreshold >= 100)
            throw new ArgumentException("Overbought threshold must be between 0 and 100.", nameof(overboughtThreshold));
        if (oversoldThreshold >= overboughtThreshold)
            throw new ArgumentException("Oversold threshold must be less than overbought threshold.", nameof(oversoldThreshold));

        _symbol = symbol;
        _period = period;
        _oversoldThreshold = oversoldThreshold;
        _overboughtThreshold = overboughtThreshold;
    }

    public Signal Update(MarketData data)
    {
        if (!string.Equals(data.Symbol, _symbol, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Strategy configured for {_symbol} cannot process {data.Symbol}.");

        if (_previousClose is null)
        {
            _previousClose = data.Close;
            return Signal.Hold(data.Symbol, data.Timestamp);
        }

        var change = data.Close - _previousClose.Value;
        _previousClose = data.Close;

        _changes.Enqueue(change);

        var gain = change > 0 ? change : 0m;
        var loss = change < 0 ? Math.Abs(change) : 0m;

        // Baseline phase: accumulate initial window
        if (!_isBaselineEstablished)
        {
            _runningGainsSum += gain;
            _runningLossesSum += loss;

            if (_changes.Count < _period)
                return Signal.Hold(data.Symbol, data.Timestamp);

            _isBaselineEstablished = true;
            return EvaluateSignal(data, _runningGainsSum / _period, _runningLossesSum / _period);
        }

        // Wilder's Smoothing phase
        var oldChange = _changes.Dequeue();
        var oldGain = oldChange > 0 ? oldChange : 0m;
        var oldLoss = oldChange < 0 ? Math.Abs(oldChange) : 0m;

        _runningGainsSum = ((_runningGainsSum - oldGain) * (_period - 1) + gain) / _period;
        _runningLossesSum = ((_runningLossesSum - oldLoss) * (_period - 1) + loss) / _period;

        return EvaluateSignal(data, _runningGainsSum, _runningLossesSum);
    }

    private Signal EvaluateSignal(MarketData data, decimal avgGain, decimal avgLoss)
    {
        // No losses means vertical pump — RSI is 100, extremely overbought
        if (avgLoss == 0)
        {
            if (100m > _overboughtThreshold)
                return Signal.Sell(data.Symbol, data.Timestamp, 0.6m, "RSI Overbought");

            return Signal.Hold(data.Symbol, data.Timestamp);
        }

        var rs = avgGain / avgLoss;
        var rsi = 100m - (100m / (1m + rs));

        if (rsi < _oversoldThreshold)
            return Signal.Buy(data.Symbol, data.Timestamp, 0.6m, "RSI Oversold");

        if (rsi > _overboughtThreshold)
            return Signal.Sell(data.Symbol, data.Timestamp, 0.6m, "RSI Overbought");

        return Signal.Hold(data.Symbol, data.Timestamp);
    }
}
