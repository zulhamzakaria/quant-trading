using QuantTrading.Simulation.Analytics;
using QuantTrading.Simulation.Models;

namespace QuantTrading.Simulation.Reporting;

public sealed class BacktestReporter
{
    public void PrintReport(
        IReadOnlyList<CompletedTrade> trades,
        decimal startingCapital,
        decimal endingPortfolioValue,
        decimal buyAndHoldReturn,
        DateTime firstBarTimestamp,
        DateTime lastBarTimestamp)
    {

        var metrics = MetricsCalculator.Calculate(
            trades,
            startingCapital,
            endingPortfolioValue,
            firstBarTimestamp,
            lastBarTimestamp);

        Console.WriteLine();
        Console.WriteLine("========== Backtest Report ==========");
        Console.WriteLine();

        Console.WriteLine("--- Period ---");
        Console.WriteLine($"From                     : {firstBarTimestamp:yyyy-MM-dd}");
        Console.WriteLine($"To                       : {lastBarTimestamp:yyyy-MM-dd}");
        Console.WriteLine($"Duration                 : {metrics.TotalYears:F2} years");
        Console.WriteLine();

        Console.WriteLine("--- Capital ---");
        Console.WriteLine($"Starting Capital         : {startingCapital:F2}");
        Console.WriteLine($"Ending Portfolio Value   : {endingPortfolioValue:F2}");
        Console.WriteLine($"Total Return             : {metrics.TotalYears:F2}%");
        Console.WriteLine($"CAGR                     : {metrics.Cagr:F2}%");
        Console.WriteLine();

        // ── Buy & Hold Benchmark ──────────────────────────────────────────
        Console.WriteLine("--- Buy & Hold Benchmark ---");
        Console.WriteLine($"Buy & Hold Return        : {buyAndHoldReturn:F2}%");
        Console.WriteLine($"Strategy vs Benchmark    : {(metrics.TotalReturn - buyAndHoldReturn):F2}%");
        Console.WriteLine();

        int tradeCount = trades.Count;

        if (tradeCount == 0)
        {
            Console.WriteLine("--- Trade Statistics ---");
            Console.WriteLine("No completed trades recorded.");
            Console.WriteLine();
            Console.WriteLine("=====================================");
            return;
        }



        Console.WriteLine("--- Trade Statistics ---");
        Console.WriteLine($"Completed Trades         : {tradeCount}");
        Console.WriteLine($"Winners                  : {metrics.Winners}");
        Console.WriteLine($"Losers                   : {metrics.Losers}");
        Console.WriteLine($"Break Even               : {metrics.BreakEven}");
        Console.WriteLine();
        Console.WriteLine($"Win Rate                 : {metrics.WinRate:F2}%");
        Console.WriteLine($"Avg Gain (winner)        : {metrics.AvgGain:F2}");
        Console.WriteLine($"Avg Loss (loser)         : {metrics.AvgLoss:F2}");
        Console.WriteLine($"Expectancy (per trade)   : {metrics.Expectancy:F2}");
        Console.WriteLine($"Profit Factor            : {(metrics.ProfitFactor.HasValue ? $"{metrics.ProfitFactor:F2}" : "N/A")}");
        Console.WriteLine($"Total Realized P&L       : {metrics.TotalRealizedPnL:F2}");
        Console.WriteLine();

        Console.WriteLine("--- Drawdown ---");
        Console.WriteLine("Max Drawdown             : N/A");
        Console.WriteLine("[NOTE] Requires mark-to-market equity curve — not yet available.");
        Console.WriteLine();

        Console.WriteLine("=====================================");

    }
}
