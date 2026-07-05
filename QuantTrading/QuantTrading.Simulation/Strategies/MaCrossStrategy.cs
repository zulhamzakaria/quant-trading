using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;

namespace QuantTrading.Simulation.Strategies;

public sealed class MaCrossStrategy : IStrategy
{
    public string Name => $"MA Trend";

    public OrderRequest? OnData(
        MarketData data,
        MarketFeatures features,
        IReadonlyAccountState accountState)
    {
        bool isBullish = features.Sma5 > features.Sma20;
        bool hasPosition = accountState.HasPositionOpen(data.Symbol);

        if (isBullish && !hasPosition)
        {
            int quantity =
                (int)(accountState.Cash / data.Close);
            if (quantity <= 0)
                return null;

            return new OrderRequest(
                data.Symbol,
                OrderType.Market,
                OrderAction.Buy,
                quantity);
        }

        if (!isBullish && hasPosition)
        {
            int quantity =
                accountState.GetPositionSize(data.Symbol);
            if (quantity <= 0)
                return null;

            return new OrderRequest(
                data.Symbol,
                OrderType.Market,
                OrderAction.Sell,
                quantity);
        }
        return null;
    }

}
