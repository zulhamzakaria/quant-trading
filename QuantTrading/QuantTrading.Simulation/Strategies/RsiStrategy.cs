using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;

namespace QuantTrading.Simulation.Strategies;

public sealed class RsiStrategy : IStrategy
{
    private const decimal OversoldThreshold = 30m;
    private const decimal OverboughtThreshold = 70m;
    public string Name
         => $"RSI";

    public OrderRequest? OnData
        (MarketData data,
        MarketFeatures features,
        IReadonlyAccountState accountState)
    {
        bool hasPosition =
             accountState.HasPositionOpen(data.Symbol);

        if (features.Rsi14 < OversoldThreshold && !hasPosition)
        {
            int quantity =
                (int)(accountState.Cash / data.Close);
            if (quantity <= 0)
                return null;

            return new OrderRequest(
                data.Symbol,
                OrderType.Market,
                OrderAction.Buy,
                new SizingInstruction.FixedQuantity(quantity));
        }

        if (features.Rsi14 > OverboughtThreshold && hasPosition)
        {
            int quantity =
                accountState.GetPositionSize(data.Symbol);
            if (quantity <= 0)
                return null;

            return new OrderRequest(
                data.Symbol,
                OrderType.Market,
                OrderAction.Sell,
                new SizingInstruction.FixedQuantity(quantity));
        }
        return null;
    }
}
