namespace QuantTrading.Domain.Entities;

public sealed class Position
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal AverageEntryPrice { get; private set; }

    public bool IsOpen => Quantity > 0;
    public decimal CostBasis => Quantity * AverageEntryPrice;

    private Position() { }

    // only Portfolio(aggregate root) can create Position
    internal Position(string symbol, decimal quantity, decimal averageEntryPrice)
    {
        Id = Guid.NewGuid();
        Symbol = symbol;
        Quantity = quantity;
        AverageEntryPrice = averageEntryPrice;
    }

    public void UpdateQuantity(decimal delta, decimal price)
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

    public void ClosePosition() 
        => Quantity = 0;
}
