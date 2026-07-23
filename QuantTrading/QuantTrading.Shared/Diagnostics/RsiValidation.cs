using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;

namespace QuantTrading.Shared.Diagnostics;

// One-off validation script — NOT part of the production feature pipeline.
// Compares FeatureGenerator's real Rsi14 output against a true single-seed,
// full-history Wilder recursion, same standard ATR and ADX were validated
// against. Delete or archive after the Position Sizing checkpoint resolves.
public static class RsiValidation
{
    private const int RsiPeriod = 14;

    // Pre-registered pass/fail criteria — decided BEFORE running, not fitted
    // to the result. Same thresholds as AtrValidation, for consistency.
    private const decimal MinAcceptableCorrelation = 0.98m;
    private const decimal MaxAcceptableRelativeMae = 0.05m;
    private const decimal MaxAcceptableRelativeMaxError = 0.15m;

    public static void RunComparison
        (IReadOnlyList<MarketData> bars)
    {
        if (bars is null || bars.Count < RsiPeriod + 1)
        {
            Console.WriteLine("[RSI VALIDATION] Insufficient bars for comparison.");
            return;
        }

        var refRsi = ComputeReferenceWilderRsi(bars);

        // Real production computation — no manual copy, no staleness risk.
        var featureSeries = new FeatureGenerator().ComputeMarketFeaturesSeries(bars);
        var currentRsiByTimestamp = featureSeries
            .ToDictionary(f => f.Timestamp, f => f.Rsi14);

        var rows = new List<(
            DateTime Date,
            decimal Ref,
            decimal Current,
            decimal AbsError)>();

        for (int i = RsiPeriod; i < bars.Count; i++)
        {
            if (refRsi[i] is null)
                continue;
            if (!currentRsiByTimestamp.TryGetValue(bars[i].Timestamp, out decimal current))
                continue;   // bar predates FeatureGenerator's own warm-up window

            decimal r = refRsi[i]!.Value;
            rows.Add((bars[i].Timestamp, r, current, Math.Abs(r - current)));
        }

        if (rows.Count == 0)
        {
            Console.WriteLine("[RSI VALIDATION] No overlapping comparable bars found.");
            return;
        }

        decimal mae = rows.Average(r => r.AbsError);
        decimal rmse = (decimal)Math.Sqrt((double)rows.Select(r => r.AbsError * r.AbsError).Average());
        decimal maxError = rows.Max(r => r.AbsError);
        decimal correlation = PearsonCorrelation(
            rows.Select(r => r.Ref).ToList(),
            rows.Select(r => r.Current).ToList());

        decimal refMean = rows.Average(r => r.Ref);
        decimal relativeMae = refMean != 0 ? mae / refMean : 0m;
        decimal relativeMaxError = refMean != 0 ? maxError / refMean : 0m;

        Console.WriteLine("========== RSI Validation (Rsi14) ==========");
        Console.WriteLine($"Bars compared        : {rows.Count}");
        Console.WriteLine($"MAE                  : {mae:F6}  ({relativeMae:P2} of mean reference RSI)");
        Console.WriteLine($"RMSE                 : {rmse:F6}");
        Console.WriteLine($"Max Error            : {maxError:F6}  ({relativeMaxError:P2} of mean reference RSI)");
        Console.WriteLine($"Correlation          : {correlation:F4}");
        Console.WriteLine();
        Console.WriteLine($"Reference RSI  — min: {rows.Min(r => r.Ref):F4}, max: {rows.Max(r => r.Ref):F4}, mean: {refMean:F4}");
        Console.WriteLine($"Current RSI    — min: {rows.Min(r => r.Current):F4}, max: {rows.Max(r => r.Current):F4}, mean: {rows.Average(r => r.Current):F4}");
        Console.WriteLine();

        Console.WriteLine("--- Top 10 Largest Discrepancies ---");
        Console.WriteLine($"{"Date",-12} {"Reference",-12} {"Current",-12} {"AbsError"}");
        foreach (var row in rows.OrderByDescending(r => r.AbsError).Take(10))
            Console.WriteLine($"{row.Date,-12:yyyy-MM-dd} {row.Ref,-12:F4} {row.Current,-12:F4} {row.AbsError:F4}");
        Console.WriteLine();

        bool pass = correlation >= MinAcceptableCorrelation
            && relativeMae <= MaxAcceptableRelativeMae
            && relativeMaxError <= MaxAcceptableRelativeMaxError;

        Console.WriteLine($"Pre-registered thresholds: Correlation ≥ {MinAcceptableCorrelation:P0}, " +
            $"Relative MAE ≤ {MaxAcceptableRelativeMae:P0}, Relative Max Error ≤ {MaxAcceptableRelativeMaxError:P0}");
        Console.WriteLine($"VERDICT: {(pass ? "NEGLIGIBLE — Rsi14 fix confirmed" : "MATERIAL — Rsi14 fix did not resolve the discrepancy, investigate")}");
        Console.WriteLine("=============================================");
    }

    /*
     True Wilder recursion: seed once with the average of the first
     RsiPeriod gain/loss values, then carry the smoothed averages forward
     across all bars without resetting. Same reference standard used for
     ATR and ADX.
    */
    private static decimal?[] ComputeReferenceWilderRsi
        (IReadOnlyList<MarketData> bars)
    {
        var result = new decimal?[bars.Count];

        decimal seedGain = 0m, seedLoss = 0m;
        for (int i = 1; i <= RsiPeriod; i++)
        {
            decimal change = bars[i].Close - bars[i - 1].Close;
            if (change > 0)
                seedGain += change;
            else
                seedLoss += Math.Abs(change);
        }
        decimal avgGain = seedGain / RsiPeriod;
        decimal avgLoss = seedLoss / RsiPeriod;

        result[RsiPeriod] = RsiFrom(avgGain, avgLoss);

        for (int i = RsiPeriod + 1; i < bars.Count; i++)
        {
            decimal change = bars[i].Close - bars[i - 1].Close;
            decimal gain = change > 0 ? change : 0m;
            decimal loss = change < 0 ? Math.Abs(change) : 0m;
            avgGain = ((avgGain * (RsiPeriod - 1)) + gain) / RsiPeriod;
            avgLoss = ((avgLoss * (RsiPeriod - 1)) + loss) / RsiPeriod;
            result[i] = RsiFrom(avgGain, avgLoss);
        }

        return result;
    }

    private static decimal RsiFrom(decimal avgGain, decimal avgLoss)
    {
        if (avgLoss > 0)
        {
            decimal rs = avgGain / avgLoss;
            return 100m - (100m / (1m + rs));
        }
        if (avgGain > 0)
            return 100m;
        return 50m;
    }

    private static decimal PearsonCorrelation
        (List<decimal> x, List<decimal> y)
    {
        int n = x.Count;
        decimal meanX = x.Average();
        decimal meanY = y.Average();

        decimal cov = 0m, varX = 0m, varY = 0m;
        for (int i = 0; i < n; i++)
        {
            decimal dx = x[i] - meanX;
            decimal dy = y[i] - meanY;
            cov += dx * dy;
            varX += dx * dx;
            varY += dy * dy;
        }

        if (varX == 0 || varY == 0)
            return 0m;

        double denom = Math.Sqrt((double)(varX * varY));
        return denom != 0 ? (decimal)((double)cov / denom) : 0m;
    }
}