namespace QuantTrading.Simulation.Models;

public sealed record CompletedTrade(
    string Symbol,
    decimal EntryPrice,
    decimal ExitPrice,
    int Quantity,
    DateTime EntryTimestamp,
    DateTime ExitTimestamp)
{
    // Derived property computed from immutable execution facts.
    // Positive = profit, negative = loss.
    public decimal RealizedPnL =>
        (ExitPrice - EntryPrice) * Quantity;
};
