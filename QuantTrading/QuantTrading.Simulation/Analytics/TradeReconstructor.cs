using QuantTrading.Shared.Execution;
using QuantTrading.Simulation.Execution;

namespace QuantTrading.Simulation.Analytics;

public sealed class TradeReconstructor
{
    private sealed class OpenLot
    {
        public required decimal Price { get; init; }
        public required DateTime Timestamp { get; init; }
        public required int RemainingQuantity { get; set; }
    }

    public IReadOnlyList<ClosedTrade> Reconstruct(IReadOnlyList<FillReceipt> fills)
    {
        var closedTrades = new List<ClosedTrade>();
        var groupedFills = fills.GroupBy(f => f.Symbol);

        foreach (var symbolGroup in groupedFills)
        {
            var buyQueue = new Queue<OpenLot>();

            foreach (var fill in symbolGroup.OrderBy(f => f.Timestamp))
            {
                if (fill.Action == OrderAction.Buy)
                {
                    buyQueue.Enqueue(new OpenLot
                    {
                        Price = fill.Price,
                        Timestamp = fill.Timestamp,
                        RemainingQuantity = fill.Quantity
                    });
                }
                else if (fill.Action == OrderAction.Sell)
                {
                    int sellQtyRemaining = fill.Quantity;

                    while (sellQtyRemaining > 0 && buyQueue.Count > 0)
                    {
                        var currentBuyLot = buyQueue.Peek();
                        int matchQty = Math.Min(sellQtyRemaining, currentBuyLot.RemainingQuantity);

                        decimal pnl = (fill.Price - currentBuyLot.Price) * matchQty;

                        closedTrades.Add(new ClosedTrade(
                            Symbol: fill.Symbol,
                            EntryPrice: currentBuyLot.Price,
                            ExitPrice: fill.Price,
                            Quantity: matchQty,
                            RealizedPnL: pnl,
                            EntryTime: currentBuyLot.Timestamp,
                            ExitTime: fill.Timestamp
                        ));

                        sellQtyRemaining -= matchQty;
                        currentBuyLot.RemainingQuantity -= matchQty;

                        if (currentBuyLot.RemainingQuantity == 0)
                        {
                            buyQueue.Dequeue();
                        }
                    }
                    if (sellQtyRemaining > 0)
                    {
                        throw new InvalidOperationException(
                            $"Attempted to sell more {fill.Symbol} shares than owned.");
                    }
                }
            }
        }

        return closedTrades;
    }
}
