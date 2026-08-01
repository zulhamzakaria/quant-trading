namespace QuantTrading.Simulation.Models;

/// <summary>
/// Represents one open lot's shares — fully or partially — realized against
/// one Sell execution. NOT a full position close, and NOT one record per Sell
/// order: under FIFO lot accounting, a single Sell that spans multiple open
/// lots produces multiple CompletedTrade records, one per lot consumed, each
/// carrying that lot's own EntryPrice/EntryTimestamp and its own realized
/// Quantity, but sharing the same ExitPrice/ExitTimestamp (the one Sell
/// event). TradeCount (as consumed by MetricsCalculator) therefore reflects
/// the number of realized lot-closures, not the number of Sell executions.
/// </summary>
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
