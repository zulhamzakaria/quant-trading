using QuantTrading.Domain.Models;
using QuantTrading.Simulation.Contracts;
using QuantTrading.Simulation.Models;
using QuantTrading.Simulation.Shared;

namespace QuantTrading.Simulation.Strategies;

public sealed class RsiStrategy : IStrategy
{
    private readonly int _period;
    private readonly decimal _oversoldThreshold;
    private readonly decimal _overboughtThreshold;
    private readonly decimal _allocationPerTrade;

    private decimal _seedGainSum;
    private decimal _seedLossSum;
    private int _seedCount;

    private decimal _avgGain;
    private decimal _avgLoss;

    private decimal? _prevClose;
    private bool _isInitialized;

    public string Name
        => $"RSI({_period}) [{_oversoldThreshold}/{_overboughtThreshold}]";

    public RsiStrategy(
        int period = 14,
        decimal oversoldThreshold = 30m,
        decimal overboughtThreshold = 70m,
        decimal allocationPerTrade = 2000.0m)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException
                (nameof(period), "Period must be at least 2.");
        if (oversoldThreshold <= 0 || oversoldThreshold >= 100)
            throw new ArgumentOutOfRangeException
                (nameof(oversoldThreshold), "Oversold threshold must be between 0 and 100.");
        if (overboughtThreshold <= 0 || overboughtThreshold >= 100)
            throw new ArgumentOutOfRangeException
                (nameof(overboughtThreshold), "Overbought threshold must be between 0 and 100.");
        if (oversoldThreshold >= overboughtThreshold)
            throw new ArgumentException
                ("Oversold threshold must be less than overbought threshold.");
        if (allocationPerTrade <= 0)
            throw new ArgumentOutOfRangeException
                (nameof(allocationPerTrade), "Allocation must be greater than zero.");

        _period = period;
        _oversoldThreshold = oversoldThreshold;
        _overboughtThreshold = overboughtThreshold;
        _allocationPerTrade = allocationPerTrade;
    }

    public OrderRequest? OnData(MarketData data, IReadonlyAccountState accountState)
    {
        if (data.Close <= 0) return null;

        if (_prevClose is null)
        {
            _prevClose = data.Close;
            return default;
        }

        decimal change =
             data.Close - _prevClose.Value;
        _prevClose = data.Close;

        decimal gain = change > 0 ? change : 0m;
        decimal loss = change < 0 ? Math.Abs(change) : 0m;

        if (!_isInitialized)
        {
            _seedGainSum += gain;
            _seedLossSum += loss;
            _seedCount++;

            if (_seedCount < _period)
                return default;

            _avgGain = _seedGainSum / _period;
            _avgLoss = _seedLossSum / _period;
            _isInitialized = true;

        }
        else
        {
            _avgGain =
                ((_avgGain * (_period - 1)) + gain) / _period;
            _avgLoss =
                ((_avgLoss * (_period - 1)) + loss) / _period;

        }

        decimal rsi =
            CalculateRsi(_avgGain, _avgLoss);
        bool hasPosition =
            accountState.HasPositionOpen(data.Symbol);

        if(rsi <= _oversoldThreshold && !hasPosition)
        {
            int targetShares =
                CalculateRsiPositionSize(data.Close);
            if(targetShares > 0)
            {
                return new OrderRequest(
                    data.Symbol,
                    OrderType.Market,
                    OrderAction.Buy,
                    targetShares);
            }
        }

        if(rsi >= _overboughtThreshold && hasPosition)
        {
            int currentShares =
                accountState.GetPositionSize(data.Symbol);
            if (currentShares <= 0) return null;
            return new OrderRequest(
                data.Symbol,
                OrderType.Market,
                OrderAction.Sell,
                currentShares);
        }

        return null;

    }

    private int CalculateRsiPositionSize(decimal close)
    {
        if(close <= 0)
            return 0;
        return (int)Math.Floor(_allocationPerTrade / close);
    }

    private decimal CalculateRsi
        (decimal avgGain, decimal avgLoss)
    {
        if (avgLoss == 0)
            return avgGain > 0 ? 100m : 50m;
        if (avgGain == 0)
            return 0m;

        decimal rs = avgGain / avgLoss;
        return 100m - (100m / (1m +  rs));
    }
}
