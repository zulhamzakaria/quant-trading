using QuantTrading.Simulation.Models;

namespace QuantTrading.Simulation.Analytics;

public static class MetricsCalculator
{
    public static StrategyMetrics Calculate(
        IReadOnlyCollection<CompletedTrade> trades,
        decimal startingCapital,
        DateTime firstBarTimestamp,
        DateTime lastBarTimestamp,
        IReadOnlyList<EquityPoint> equityCurve)
    {
        if (trades is null)
            throw new ArgumentNullException(nameof(trades));
        if (startingCapital <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(startingCapital),
                "Starting capital must be greater than zero.");
        if (lastBarTimestamp < firstBarTimestamp)
            throw new ArgumentException(
                "lastBarTimestamp must not precede firstBarTimestamp.",
                nameof(lastBarTimestamp));
        // Single source of truth: endingPortfolioValue is no longer a
        // separately-supplied parameter — it is always the equity curve's
        // final point. Removes the possibility of a caller passing
        // inconsistent ending-value/equity-curve data. Requires a non-empty
        // equityCurve; BacktestEngine appends one point per processed bar,
        // so this holds for any simulation that ran at least one bar.
        if (equityCurve is null || equityCurve.Count == 0)
            throw new ArgumentException(
                "equityCurve must contain at least one point; it is now the " +
                "sole source of the ending portfolio value.",
                nameof(equityCurve));

        decimal endingPortfolioValue = equityCurve[^1].Equity;

        double totalDays =
            (lastBarTimestamp - firstBarTimestamp).TotalDays;
        double totalYears = totalDays / 365.25;

        decimal totalReturn =
            (endingPortfolioValue - startingCapital) / startingCapital * 100m;

        double cagr = CalculateCagr(
            startingCapital,
            endingPortfolioValue,
            totalYears);

        decimal? maxDrawdownPercent =
            CalculateMaxDrawdown(equityCurve);

        int tradeCount = trades.Count;

        if (tradeCount == 0)
        {
            return new StrategyMetrics
            {
                TradeCount = 0,
                Winners = 0,
                Losers = 0,
                BreakEven = 0,
                TotalReturn = totalReturn,
                Cagr = cagr,
                WinRate = null,
                AvgGain = null,
                AvgLoss = null,
                Expectancy = null,
                ProfitFactor = null,
                GrossProfit = 0m,
                GrossLoss = 0m,
                TotalRealizedPnL = 0m,
                TotalYears = totalYears,
                MaxDrawdownPercent = maxDrawdownPercent,
            };
        }

        var winners = trades.Where(t => t.RealizedPnL > 0).ToList();
        var losers = trades.Where(t => t.RealizedPnL < 0).ToList();
        var breakEven = trades.Where(t => t.RealizedPnL == 0).ToList();

        // WinRate excludes break-even trades from both numerator and
        // denominator (Wins / (Wins + Losses)) — a break-even trade is
        // neither a win nor a loss, and should not dilute the rate. Null
        // when there are no decisive (win or loss) trades at all.
        int decisiveTradeCount = winners.Count + losers.Count;
        decimal? winRate = decisiveTradeCount > 0
            ? (decimal)winners.Count / decisiveTradeCount * 100m
            : null;

        decimal? avgGain = winners.Count > 0
            ? winners.Average(t => t.RealizedPnL)
            : null;

        // AvgLoss is reported as a positive magnitude, consistent with
        // GrossLoss's existing Math.Abs convention — previously negative-
        // signed with no other field agreeing on that convention.
        decimal? avgLoss = losers.Count > 0
            ? Math.Abs(losers.Average(t => t.RealizedPnL))
            : null;

        decimal grossProfit = winners.Sum(t => t.RealizedPnL);
        decimal grossLoss = Math.Abs(losers.Sum(t => t.RealizedPnL));

        decimal? profitFactor = grossLoss > 0
            ? grossProfit / grossLoss
            : null;

        // Expectancy intentionally uses TOTAL trade count (including
        // break-evens) as its denominator, not the decisive-trade count
        // WinRate now uses — Expectancy means "average $ per trade taken,"
        // and a break-even trade is a legitimate zero-outcome trade for
        // that purpose. Deliberate difference from WinRate's denominator,
        // not an inconsistency.
        decimal? expectancy = null;
        if (avgGain.HasValue || avgLoss.HasValue)
        {
            decimal winRateOfTotal = 
                (decimal)winners.Count / tradeCount;
            decimal lossRateOfTotal =
                (decimal)losers.Count / tradeCount;
            decimal winContribution =
                (winRateOfTotal) * (avgGain ?? 0m);
            // avgLoss is now a positive magnitude, so its contribution to
            // expectancy must be explicitly negated.
            decimal lossContribution =
                -lossRateOfTotal * (avgLoss ?? 0m);
            expectancy = winContribution + lossContribution;
        }

        decimal totalRealizedPnL = trades.Sum(t => t.RealizedPnL);

        return new StrategyMetrics
        {
            TradeCount = tradeCount,
            Winners = winners.Count,
            Losers = losers.Count,
            BreakEven = breakEven.Count,
            TotalReturn = totalReturn,
            Cagr = cagr,
            WinRate = winRate,
            AvgGain = avgGain,
            AvgLoss = avgLoss,
            Expectancy = expectancy,
            ProfitFactor = profitFactor,
            GrossProfit = grossProfit,
            GrossLoss = grossLoss,
            TotalRealizedPnL = totalRealizedPnL,
            TotalYears = totalYears,
            MaxDrawdownPercent = maxDrawdownPercent,
        };
    }

    private static decimal? CalculateMaxDrawdown
        (IReadOnlyList<EquityPoint> equityCurve)
    {
        if (equityCurve is null || equityCurve.Count == 0)
            return null;

        decimal peak = equityCurve[0].Equity;
        decimal maxDrawdown = 0m;

        foreach (var point in equityCurve)
        {
            if (point.Equity > peak)
            {
                peak = point.Equity;
            }
            else if (peak > 0)
            {
                decimal drawdown =
                    (peak - point.Equity) / peak * 100m;
                if (drawdown > maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }
        }

        return maxDrawdown;
    }

    private static double CalculateCagr(
        decimal startingCapital,
        decimal endingPortfolioValue,
        double totalYears)
    {
        if (totalYears <= 0)
            return 0.0;

        if (endingPortfolioValue <= 0)
            return double.NaN;

        double ratio =
            (double)(endingPortfolioValue / startingCapital);
        return (Math.Pow(ratio, 1.0 / totalYears) - 1.0) * 100.0;
    }

}
