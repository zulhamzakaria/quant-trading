using QuantTrading.Simulation.Analytics;
using QuantTrading.Simulation.Models;

namespace QuantTrading.Simulation.Reporting;

public sealed class TournamentReporter
{
    private readonly BacktestReporter _backtestReporter = new();

    // TODO(Checkpoint 1 / Phase 4): PROVISIONAL — not yet a decided value.
    // Below this trade count, results are flagged as low-sample per the
    // acceptance criteria ("sufficient number of trades to avoid overfitting
    // to one or two lucky trades"). No formal minimum has been agreed yet;
    // this exists only so the flag has *some* threshold to render. Replace
    // once a real value is decided as part of Checkpoint 1.
    private const int ProvisionalMinimumTradesThreshold = 10;
    public void PrintReport
        (IReadOnlyList<StrategyResult> results)
    {
        if (results is null || results.Count == 0)
        {
            throw new ArgumentException(
                "No strategy results to report.",
                nameof(results));
        }

        var buyAndHoldResult = results
            .FirstOrDefault(r => r.StrategyName == "Buy & Hold");
        decimal buyAndHoldReturn =
            buyAndHoldResult?.TotalReturn ?? 0m;
        if (buyAndHoldResult is null)
            Console.WriteLine("[NOTE] 'Buy & Hold' not present in this run — benchmark comparisons below are against 0%, not a real baseline.");

        var allMetrics = results
            .Select(r => (Result: r, Metrics: MetricsCalculator.Calculate(
                r.Trades,
                r.StartingCapital,
                r.EndingPortfolioValue,
                r.FirstBarTimestamp,
                r.LastBarTimestamp,
                r.EquityCurve)))
            .ToList();

        foreach (var (result, metrics) in allMetrics)
        {
            Console.WriteLine();
            Console.WriteLine($"Strategy: {result.StrategyName}");
            Console.WriteLine();

            _backtestReporter.PrintReport(
                metrics,
                result.StartingCapital,
                result.EndingPortfolioValue,
                buyAndHoldReturn,
                result.FirstBarTimestamp,
                result.LastBarTimestamp);

            var drawdownAudit = DrawdownAuditReporter.Analyze(result.EquityCurve);
            Console.WriteLine(
                $"Drawdown Audit  : Peak {drawdownAudit.PeakValue:F2} on {drawdownAudit.PeakDate:yyyy-MM-dd} " +
                $"→ Trough {drawdownAudit.TroughValue:F2} on {drawdownAudit.TroughDate:yyyy-MM-dd} " +
                $"({drawdownAudit.MaxDrawdownPct:P2})");

        }

        PrintComparativeSummary(allMetrics);
    }

    private void PrintComparativeSummary
        (List<(StrategyResult Result, StrategyMetrics Metrics)> allMetrics)
    {
        Console.WriteLine();
        Console.WriteLine("========== Tournament Summary ==========");
        Console.WriteLine();
        Console.WriteLine(
            $"{"Strategy",-22} " +
            $"{"Total Return",-16} " +
            $"{"CAGR",-12} " +
            $"{"Trades",-10} " +
            $"{"Win Rate",-12} " +
            $"{"Profit Factor",-16} " +
            $"{"Expectancy",-12} " +
            $"{"Max Drawdown"}");
        Console.WriteLine(new string('-', 100));

        //rank is based on CAGR
        var ranked = allMetrics
            .OrderByDescending(x => double.IsNaN(x.Metrics.Cagr) ? double.MinValue : x.Metrics.Cagr);


        foreach (var (result, metrics) in ranked)
        {
            string cagrCol =
                double.IsNaN(metrics.Cagr)
                ? "N/A"
                : $"{metrics.Cagr:F2}%";
            string tradeCountCol =
                metrics.TradeCount < ProvisionalMinimumTradesThreshold
                ? $"{metrics.TradeCount}*"
                : $"{metrics.TradeCount}";

            if (metrics.TradeCount == 0)
            {
                string zeroTradeReturnCol = $"{metrics.TotalReturn:F2}%";
                string zeroTradeDrawdownCol = 
                    metrics.MaxDrawdownPercent.HasValue
                    ? $"{metrics.MaxDrawdownPercent:F2}%"
                    : "N/A";
                Console.WriteLine(
                    $"{result.StrategyName,-22} " +
                    $"{zeroTradeReturnCol,-14} " +
                    $"{cagrCol,-10} {tradeCountCol,-8} " +
                    $"{"N/A",-10} {"N/A",-14} {"N/A",-12} {zeroTradeDrawdownCol}");
                continue;
            }

            string winRateCol = $"{metrics.WinRate:F2}% ";
            string profitFactorCol = metrics.ProfitFactor.HasValue
                ? $"{metrics.ProfitFactor.Value:F2}"
                : "N/A";
            string expectancyCol = metrics.Expectancy.HasValue
                ? $"{metrics.Expectancy.Value:F2}"
                : "N/A";
            string totalReturnCol = $"{metrics.TotalReturn:F2}%";
            string drawdownCol = 
                metrics.MaxDrawdownPercent.HasValue
                ? $"{metrics.MaxDrawdownPercent:F2}%"
                : "N/A";

            Console.WriteLine(
                $"{result.StrategyName,-22} " +
                $"{totalReturnCol,-14} " +
                $"{cagrCol,-10} {tradeCountCol,-8} " +
                $"{winRateCol,-10} {profitFactorCol,-14} {expectancyCol,-12} {drawdownCol}");
        }

        Console.WriteLine();
        Console.WriteLine($"[NOTE] '*' next to trade count indicates fewer than {ProvisionalMinimumTradesThreshold} trades — low sample size, treat results cautiously. Threshold is PROVISIONAL, not yet formally decided.");
        Console.WriteLine("========================================");
    }
}
