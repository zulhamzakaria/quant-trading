using QuantTrading.Application.Models;
using QuantTrading.Simulation.Contracts;
using QuantTrading.Simulation.Models;
using QuantTrading.Simulation.Shared;

namespace QuantTrading.Simulation.Strategies;

public sealed class DummyBuyAndHoldStrategy : IStrategy
{
    public string Name => "Dummy Buy and Hold Verification Strategy";

    public OrderRequest? OnData(MarketData data, IReadonlyAccountState accountState)
    {
        if (!accountState.HasPositionOpen(data.Symbol))
        {
            Console.WriteLine($"[Strategy] {data.Symbol} Position Size = {accountState.GetPositionSize(data.Symbol)}");
            return new OrderRequest(
                 data.Symbol,
                 OrderType.Market,
                 OrderAction.Buy,
                 10);
        }
        return null;
    }
}
