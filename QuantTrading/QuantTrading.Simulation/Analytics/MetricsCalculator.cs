using QuantTrading.Simulation.Models;

namespace QuantTrading.Simulation.Analytics;

public static class MetricsCalculator
{
    public static StrategyMetrics Calculate(
        IReadOnlyCollection<CompletedTrade> trades,
        decimal startingCapital,
        decimal endingPortfolioValue,
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

        decimal winRate = (decimal)winners.Count / tradeCount * 100m;

        decimal? avgGain = winners.Count > 0
            ? winners.Average(t => t.RealizedPnL)
            : null;
        decimal? avgLoss = losers.Count > 0
            ? losers.Average(t => t.RealizedPnL)
            : null;

        decimal grossProfit = winners.Sum(t => t.RealizedPnL);
        decimal grossLoss = Math.Abs(losers.Sum(t => t.RealizedPnL));

        decimal? profitFactor = grossLoss > 0
            ? grossProfit / grossLoss
            : null;

        decimal? expectancy = null;
        if (avgGain.HasValue || avgLoss.HasValue)
        {
            decimal lossRate =
                (decimal)losers.Count / tradeCount;
            decimal winContribution =
                (winRate / 100m) * (avgGain ?? 0m);
            decimal lossContribution =
                lossRate * (avgLoss ?? 0m);
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
