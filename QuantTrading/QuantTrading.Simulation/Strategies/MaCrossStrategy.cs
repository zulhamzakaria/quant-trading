using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;

namespace QuantTrading.Simulation.Strategies;

public sealed class MaCrossStrategy : IStrategy
{
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private readonly decimal _allocationPerTrade;

    private readonly Queue<decimal> _fastWindow = new();
    private readonly Queue<decimal> _slowWindow = new();
    private decimal _fastSum;
    private decimal _slowSum;

    private bool? _previousFastAboveSlow;

    public string Name
        => $"ma-cross-strategy({_fastPeriod}/{_slowPeriod})";

    public MaCrossStrategy(
        int fastPeriod = 10,
        int slowPeriod = 20,
        decimal allocationPerTrade = 2000.0m)
    {
        if (fastPeriod < 2)
            throw new ArgumentOutOfRangeException
                (nameof(fastPeriod), "Fast period must be at least 2.");
        if (slowPeriod <= fastPeriod)
            throw new ArgumentOutOfRangeException
                (nameof(slowPeriod), "Slow period must be greater than fast period.");
        if (allocationPerTrade <= 0)
            throw new ArgumentOutOfRangeException
                (nameof(allocationPerTrade), "Allocation per trade must be positive.");
        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;
        _allocationPerTrade = allocationPerTrade;
    }

    public OrderRequest? OnData
        (MarketData data, MarketFeatures features, IReadonlyAccountState accountState)
    {
        if (data.Close <= 0)
            return null;
        UpdateFastWindow(data.Close);
        UpdateSlowWindow(data.Close);

        if (_slowWindow.Count < _slowPeriod)
            return null;

        decimal fastMa = _fastSum / _fastPeriod;
        decimal slowMa = _slowSum / _slowPeriod;
        bool fastAboveSlow = fastMa > slowMa;

        OrderRequest? request = default;

        if (_previousFastAboveSlow.HasValue)
        {
            bool goldenCross =
                !_previousFastAboveSlow.Value && fastAboveSlow;
            bool deathCross =
                _previousFastAboveSlow.Value && !fastAboveSlow;

            int targetShares =
                (int)Math.Floor(_allocationPerTrade / data.Close);

            if (goldenCross
                    && !accountState.HasPositionOpen(data.Symbol)
                    && targetShares > 0)
            {
                request = new OrderRequest(
                    data.Symbol,
                    OrderType.Market,
                    OrderAction.Buy,
                    targetShares);
            }
            else if (deathCross
                    && accountState.HasPositionOpen(data.Symbol))
            {
                int currentShares =
                    (int)accountState.GetPositionSize(data.Symbol);
                request = new OrderRequest(
                    data.Symbol,
                    OrderType.Market,
                    OrderAction.Sell,
                    currentShares);
            }
        }
        _previousFastAboveSlow = fastAboveSlow;
        return request;
    }

    private void UpdateSlowWindow(decimal close)
    {
        _slowWindow.Enqueue(close);
        _slowSum += close;
        if (_slowWindow.Count > _slowPeriod)
            _slowSum -= _slowWindow.Dequeue();
    }

    private void UpdateFastWindow(decimal close)
    {
        _fastWindow.Enqueue(close);
        _fastSum += close;
        if (_fastWindow.Count > _fastPeriod)
            _fastSum -= _fastWindow.Dequeue();
    }
}
