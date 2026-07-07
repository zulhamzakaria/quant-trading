using QuantTrading.Simulation.Models;

namespace QuantTrading.Simulation.Reporting;

public sealed class TournamentReporter
{
    private readonly BacktestReporter _backtestReporter = new();

    public void PrintReport
        (IReadOnlyList<StrategyResult> results)
    {
        if (results is null || results.Count == 0)
        {
            throw new ArgumentException(
                "No strategy results to report.",
                nameof(results));
        }

        decimal buyAndHoldReturn = results
            .FirstOrDefault(r => r.StrategyName == "Buy & Hold")?
            .TotalReturn ?? 0m;

        foreach (var result in results)
        {
            Console.WriteLine();
            Console.WriteLine($"Strategy: {result.StrategyName}");
            Console.WriteLine();

            _backtestReporter.PrintReport(
                result.Trades,
                result.StartingCapital,
                result.EndingPortfolioValue,
                buyAndHoldReturn,
                result.FirstBarTimestamp,
                result.LastBarTimestamp);
        }
        PrintComparativeSummary(results);
    }

    private void PrintComparativeSummary
        (IReadOnlyList<StrategyResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("========== Tournament Summary ==========");
        Console.WriteLine();
        Console.WriteLine(
            $"{"Strategy",-22} {"Total Return",-16} {"CAGR",-12} {"Trades",-10} {"Win Rate",-12} {"Profit Factor",-16} {"Max Drawdown"}");
        Console.WriteLine(new string('-', 100));

        foreach (var result in results.
            OrderByDescending(r => r.TotalReturn))
        {
            double totalDays =
                (result.LastBarTimestamp - result.FirstBarTimestamp)
                .TotalDays;
            double totalYears = totalDays / 365.25;

            double cagr = totalYears > 0 ?
                (Math.Pow((double)(result.EndingPortfolioValue / result.StartingCapital), 1 / totalYears) - 1) * 100.0
                : 0;

            int tradeCount = result.Trades.Count;

            if (tradeCount == 0)
            {
                Console.WriteLine(
                     $"{result.StrategyName,-22} " +
                     $"{result.TotalReturn,-16:F2}% " +
                     $"{cagr,-12:F2}% {"0",-10} {"N/A",-12} " +
                     $"{"N/A",-16} " +
                     $"{"N/A"}");
                continue;
            }

            var winners = result.Trades
                .Where(t => t.RealizedPnL > 0)
                .ToList();
            var losers = result.Trades
                .Where(t => t.RealizedPnL < 0)
                .ToList();

            decimal winRate =
                (decimal)winners.Count / tradeCount * 100m;

            decimal grossProfit =
                winners.Sum(t => t.RealizedPnL);
            decimal grossLoss =
                Math.Abs(losers.Sum(t => t.RealizedPnL));

            string profitFactor = grossLoss > 0
                ? (grossProfit / grossLoss).ToString("F2")
                : "N/A";

            // no drawdown calculation here,
            // as it requires equity curve data
            string totalReturnCol = $"{result.TotalReturn:F2}%";
            string cagrCol = $"{cagr:F2}%";
            string winRateCol = $"{winRate:F2}%";

            Console.WriteLine($"{result.StrategyName,-22} " +
                $"{totalReturnCol,-16} " +
                $"{cagrCol,-12} {tradeCount,-10} " +
                $"{winRateCol,-12} {profitFactor,-16} " +
                $"n/a");

        }
        Console.WriteLine();
        Console.WriteLine("[NOTE] Max Drawdown requires mark-to-market equity curve — not yet available.");
        Console.WriteLine("========================================");
    }
}
