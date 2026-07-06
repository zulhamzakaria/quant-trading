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
        if (trades is null)
            throw new ArgumentNullException(nameof(trades));
        if (startingCapital <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(startingCapital),
                "Starting capital must be greater than zero.");

        Console.WriteLine();
        Console.WriteLine("========== Backtest Report ==========");
        Console.WriteLine();

        double totalDays =
            (lastBarTimestamp - firstBarTimestamp).TotalDays;
        double totalYears = totalDays / 365.25;

        Console.WriteLine("--- Period ---");
        Console.WriteLine($"From                     : {firstBarTimestamp:yyyy-MM-dd}");
        Console.WriteLine($"To                       : {lastBarTimestamp:yyyy-MM-dd}");
        Console.WriteLine($"Duration                 : {totalYears:F2} years");
        Console.WriteLine();

        decimal totalReturn =
            (endingPortfolioValue - startingCapital) / startingCapital * 100m;

        double cagr = totalYears > 0
            ? (Math.Pow((double)(endingPortfolioValue / startingCapital), 1.0 / totalYears) - 1.0) * 100.0
            : 0.0;

        Console.WriteLine("--- Capital ---");
        Console.WriteLine($"Starting Capital         : {startingCapital:F2}");
        Console.WriteLine($"Ending Portfolio Value   : {endingPortfolioValue:F2}");
        Console.WriteLine($"Total Return             : {totalReturn:F2}%");
        Console.WriteLine($"CAGR                     : {cagr:F2}%");
        Console.WriteLine();

        // ── Buy & Hold Benchmark ──────────────────────────────────────────
        Console.WriteLine("--- Buy & Hold Benchmark ---");
        Console.WriteLine($"Buy & Hold Return        : {buyAndHoldReturn:F2}%");
        Console.WriteLine($"Strategy vs Benchmark    : {(totalReturn - buyAndHoldReturn):F2}%");
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

        var winners =
            trades.Where(t => t.RealizedPnL > 0).ToList();
        var losers =
            trades.Where(t => t.RealizedPnL < 0).ToList();
        var breakEven =
            trades.Where(t => t.RealizedPnL == 0).ToList();

        decimal winRate =
            (decimal)winners.Count / tradeCount * 100m;

        decimal avgGain = winners.Count > 0
            ? winners.Average(t => t.RealizedPnL)
            : 0m;

        decimal avgLoss = losers.Count > 0
            ? losers.Average(t => t.RealizedPnL)
            : 0m;

        decimal grossProfit =
            winners.Sum(t => t.RealizedPnL);
        decimal grossLoss =
            Math.Abs(losers.Sum(t => t.RealizedPnL));

        string profitFactor = grossLoss > 0
            ? (grossProfit / grossLoss).ToString("F2")
            : "N/A";

        decimal totalRealizedPnL =
            trades.Sum(t => t.RealizedPnL);

        Console.WriteLine("--- Trade Statistics ---");
        Console.WriteLine($"Completed Trades         : {tradeCount}");
        Console.WriteLine($"Winners                  : {winners.Count}");
        Console.WriteLine($"Losers                   : {losers.Count}");
        Console.WriteLine($"Break Even               : {breakEven.Count}");
        Console.WriteLine();
        Console.WriteLine($"Win Rate                 : {winRate:F2}%");
        Console.WriteLine($"Avg Gain (winner)        : {avgGain:F2}");
        Console.WriteLine($"Avg Loss (loser)         : {avgLoss:F2}");
        Console.WriteLine($"Profit Factor            : {profitFactor}");
        Console.WriteLine($"Total Realized P&L       : {totalRealizedPnL:F2}");
        Console.WriteLine();

        Console.WriteLine("--- Drawdown ---");
        Console.WriteLine("Max Drawdown             : N/A");
        Console.WriteLine("[NOTE] Requires mark-to-market equity curve — not yet available.");
        Console.WriteLine();

        Console.WriteLine("=====================================");

    }
}
