namespace QuantTrading.Simulation.Models;

public sealed record CompletedTrade(
    string Symbol,
    decimal EntryPrice,
    decimal ExitPrice,
    int Quantity,
    DateTime EntryTimestamp,
    DateTime ExitTimestamp);
