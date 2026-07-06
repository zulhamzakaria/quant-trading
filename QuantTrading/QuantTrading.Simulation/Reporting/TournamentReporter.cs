using QuantTrading.Simulation.Models;

namespace QuantTrading.Simulation.Reporting;

public sealed class TournamentReporter
{
    private readonly BacktestReporter _backtestReporter = new();

    public void PrintReport
        (IReadOnlyList<StrategyResult> results)
    {
        if(results is null || results.Count == 0)
        {
            throw new ArgumentException(
                "No strategy results to report.",
                nameof(results));
        }

        decimal buyAndHoldReturn = results
            .FirstOrDefault(r => r.StrategyName == "Buy & Hold")?
            .TotalReturn ?? 0m;
        
        foreach(var result in results)
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

    }
}
