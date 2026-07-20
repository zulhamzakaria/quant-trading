namespace QuantTrading.Simulation.Models;

public sealed record StrategyResult(
    string StrategyName,
    IReadOnlyList<CompletedTrade> Trades,
    decimal StartingCapital,
    decimal EndingPortfolioValue,
    DateTime FirstBarTimestamp,
    DateTime LastBarTimestamp,
    IReadOnlyList<EquityPoint> EquityCurve)
{
    public decimal TotalReturn =>
        (EndingPortfolioValue - StartingCapital) / StartingCapital * 100m;
};

public sealed record EquityPoint(
    DateTime Timestamp,
    decimal Equity);