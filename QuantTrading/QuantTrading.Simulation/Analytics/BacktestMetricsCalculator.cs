namespace QuantTrading.Simulation.Analytics;

public sealed class BacktestMetricsCalculator
{
    public BacktestMetrics Calculate(IReadOnlyList<ClosedTrade> closedTrades)
    {
        int totalTrades = closedTrades.Count;
        decimal winRate = totalTrades > 0
            ? ((decimal)closedTrades.Count(t => t.IsWin) / totalTrades) * 100
            : 0m;

        return new BacktestMetrics(totalTrades, winRate);
    }
}
