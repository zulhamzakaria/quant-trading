using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;

namespace QuantTrading.Simulation.Strategies;

public sealed class BollingerBandsStrategy : IStrategy
{
    private const decimal BandMultiplier = 2m;

    public string Name => "Bollinger Bands";

    public OrderRequest? OnData(
        MarketData data,
        MarketFeatures features,
        IReadonlyAccountState accountState)
    {
        decimal upperBand =
            features.Sma20 + BandMultiplier * features.BollingerStdDev20;
        decimal lowerBand =
            features.Sma20 - BandMultiplier * features.BollingerStdDev20;

        bool hasPosition =
            accountState.HasPositionOpen(data.Symbol);

        if (data.Close < lowerBand && !hasPosition)
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

        if(data.Close > upperBand && hasPosition)
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
