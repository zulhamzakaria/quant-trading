namespace QuantTrading.Simulation.Models;

public sealed record StrategyResult(
    string StrategyName,
    IReadOnlyList<CompletedTrade> Trades,
    decimal StartingCapital,
    decimal EndingPortfolioValue,
    DateTime FirstBarTimestamp,
    DateTime LastBarTimestamp);
