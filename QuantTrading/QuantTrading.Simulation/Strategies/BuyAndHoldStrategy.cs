using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;

namespace QuantTrading.Simulation.Strategies;

public sealed class BuyAndHoldStrategy : IStrategy
{
    public string Name => "Buy & Hold";

    public OrderRequest? OnData(MarketData data, MarketFeatures features, IReadonlyAccountState accountState)
    {
        if (accountState.HasPositionOpen(data.Symbol))
            return null;

        int quantity = (int)(accountState.Cash / data.Close);

        if (quantity <= 0)
            return null;

        return new OrderRequest(
            data.Symbol,
            OrderType.Market,
            OrderAction.Buy,
            quantity);
    }
}
