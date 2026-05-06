namespace QuantTrading.Domain.Entities;

public sealed class Position
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal  AverageEntryPrice { get; private set; }

    private Position() { }

    // only Portfolio(aggregate root) can create Position
    internal Position(string symbol, decimal quantity, decimal averageEntryPrice)
    {
        Id = Guid.NewGuid();
        Symbol = symbol;
        Quantity = quantity;
        AverageEntryPrice = averageEntryPrice;
    }

    /// <summary>
    /// Average Entry Price	:
    /// The weighted-average price you paid to acquire the current position. 
    /// Used as the cost basis for P&L and tax calculations.
    /// 
    /// Cost Basis :
    /// Total amount paid to acquire the asset. 
    /// Selling part doesn't change the per-unit cost basis of what you still hold.
    /// 
    /// Delta :	
    /// A signed change in quantity — positive for buy, negative for sell. 
    /// "Delta" is overused in quant finance, but here it just means "change in position size."
    /// 
    /// Quantity :
    /// How many units (shares, contracts, coins) you currently hold.
    /// 
    /// Weighted Average :	
    /// Gives more influence to larger purchases.
    /// 1 share@10 vs 1k shares@10
    /// </summary>
    /// <param name="delta"></param>
    /// <param name="price"></param>

    internal void UpdateQuantity(decimal delta, decimal price)
    {
        if (delta > 0) // Case: Buying more of the same asset
        {
            // Math: (Existing Value + New Value) / New Total Quantity
            decimal totalCost = (Quantity * AverageEntryPrice) + (delta * price);
            Quantity += delta;
            AverageEntryPrice = totalCost / Quantity;
        }
        else // Case: Selling part of the asset
        {
            // When selling, the AverageEntryPrice (Cost Basis) does not change.
            // We only reduce the total quantity.
            Quantity += delta; // delta is negative
        }
    }

    public decimal GetMarketValue(decimal currentPrice) 
        => Quantity * currentPrice;

    public decimal GetUnrealizedPnL(decimal currentPrice)
        => (currentPrice - AverageEntryPrice) * Quantity;
}
