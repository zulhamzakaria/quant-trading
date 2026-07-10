namespace QuantTrading.Simulation.Analytics;

// to hold calculated performance metris
// calculate once and is the single source of truth
public sealed record StrategyMetrics
{
    public required int TradeCount { get; init; }
    public required int Winners { get; init; }
    public required int Losers { get; init; }
    public required int BreakEven { get; init; }

    public required decimal TotalReturn { get; init; }
    public required double Cagr { get; init; }

    public decimal? WinRate { get; init; }
    public decimal? AvgGain { get; init; }
    public decimal? AvgLoss { get; init; }
    public decimal? Expectancy { get; init; }
    public decimal? ProfitFactor { get; init; }

    public required decimal GrossProfit { get; init; }
    public required decimal GrossLoss { get; init; }
    public required decimal TotalRealizedPnL { get; init; }

    public required double TotalYears { get; init; }
}
