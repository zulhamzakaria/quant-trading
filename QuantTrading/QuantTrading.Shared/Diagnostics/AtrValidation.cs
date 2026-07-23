using QuantTrading.Shared.Models;

namespace QuantTrading.Shared.Diagnostics;

public static class AtrValidation
{
    private const int AtrPeriod = 14;

    // Pre-registered pass/fail criteria — decided BEFORE running, not fitted
    // to the result. See handoff doc Open Questions for rationale.
    private const decimal MinAcceptableCorrelation = 0.98m;
    private const decimal MaxAcceptableRelativeMae = 0.05m;   // 5% of mean reference ATR
    private const decimal MaxAcceptableRelativeMaxError = 0.15m; // 15% of mean reference ATR
    public static void RunComparison
        (IReadOnlyList<MarketData> bars)
    {
        if (bars is null || bars.Count < AtrPeriod + 1)
        {
            Console.WriteLine("[ATR VALIDATION] Insufficient bars for comparison.");
            return;
        }

        var refAtr = ComputeReferenceWilderAtr(bars);
        var currentAtr = ComputeCurrentImplementationAtr(bars);

        var rows = new List<(
            DateTime Date,
            decimal Ref,
            decimal Current,
            decimal AbsError)>();

        for (int i = AtrPeriod; i < bars.Count; i++)
        {
            if (refAtr[i] is null || currentAtr[i] is null)
                continue;

            decimal r = refAtr[i]!.Value;
            decimal c = currentAtr[i]!.Value;
            rows.Add((bars[i].Timestamp, r, c, Math.Abs(r - c)));
        }

        if (rows.Count == 0)
        {
            Console.WriteLine("[ATR VALIDATION] No overlapping comparable bars found.");
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

        Console.WriteLine("========== ATR Validation (AtrRatio14) ==========");
        Console.WriteLine($"Bars compared        : {rows.Count}");
        Console.WriteLine($"MAE                  : {mae:F6}  ({relativeMae:P2} of mean reference ATR)");
        Console.WriteLine($"RMSE                 : {rmse:F6}");
        Console.WriteLine($"Max Error            : {maxError:F6}  ({relativeMaxError:P2} of mean reference ATR)");
        Console.WriteLine($"Correlation          : {correlation:F4}");
        Console.WriteLine();
        Console.WriteLine($"Reference ATR  — min: {rows.Min(r => r.Ref):F6}, max: {rows.Max(r => r.Ref):F6}, mean: {refMean:F6}");
        Console.WriteLine($"Current ATR    — min: {rows.Min(r => r.Current):F6}, max: {rows.Max(r => r.Current):F6}, mean: {rows.Average(r => r.Current):F6}");
        Console.WriteLine();

        Console.WriteLine("--- Top 10 Largest Discrepancies ---");
        Console.WriteLine($"{"Date",-12} {"Reference",-12} {"Current",-12} {"AbsError"}");
        foreach (var row in rows.OrderByDescending(r => r.AbsError).Take(10))
            Console.WriteLine($"{row.Date:yyyy-MM-dd,-12} {row.Ref,-12:F6} {row.Current,-12:F6} {row.AbsError:F6}");
        Console.WriteLine();

        bool pass = correlation >= MinAcceptableCorrelation
            && relativeMae <= MaxAcceptableRelativeMae
            && relativeMaxError <= MaxAcceptableRelativeMaxError;

        Console.WriteLine($"Pre-registered thresholds: Correlation ≥ {MinAcceptableCorrelation:P0}, " +
            $"Relative MAE ≤ {MaxAcceptableRelativeMae:P0}, Relative Max Error ≤ {MaxAcceptableRelativeMaxError:P0}");
        Console.WriteLine($"VERDICT: {(pass ? "NEGLIGIBLE — proceed with Experiment 2 unchanged" : "MATERIAL — suspend Experiment 2 promotion, fix + retrain")}");
        Console.WriteLine("==================================================");
    }

    /*
     True Wilder recursion: seed once with the average of the first AtrPeriod
     True Range values, then carry the smoothed average forward across all
     bars without resetting. This is the reference standard — same method
     used to validate ADX’s 200-bar fix.
    */
    private static decimal?[] ComputeReferenceWilderAtr
        (IReadOnlyList<MarketData> bars)
    {
        var result = new decimal?[bars.Count];

        decimal seed = 0m;
        for (int i = 1; i <= AtrPeriod; i++)
        {
            seed += TrueRange(bars[i], bars[i - 1]);
        }
        seed /= AtrPeriod;

        result[AtrPeriod] =
             SafeClose(bars[AtrPeriod], out decimal seedClose)
             ? seed / seedClose
             : null;

        decimal smoothed = seed;
        for (int i = AtrPeriod + 1; i < bars.Count; i++)
        {
            decimal tr =
                TrueRange(bars[i], bars[i - 1]);
            smoothed =
                ((smoothed * (AtrPeriod - 1)) + tr) / AtrPeriod;
            result[i] = SafeClose(bars[i], out decimal close)
                ? smoothed / close
                :null;
        }

        return result;
    }

    // Snapshot of FeatureGenerator.ComputeMarketFeaturesAt’s private ATR block
    // (as reviewed for Position Sizing Checkpoint 3). This is a manual copy,
    // not production code. If FeatureGenerator.cs’s ATR logic changes, this
    // copy goes stale and must be re-synced before re-running diagnostics.
    private static decimal?[] ComputeCurrentImplementationAtr
        (IReadOnlyList<MarketData> bars)
    {
        var result = new decimal?[bars.Count];

        for (int index = AtrPeriod; index < bars.Count; index++)
        {
            decimal avgAtr = 0m;
            int startAtrIndex = index - AtrPeriod;
            if (startAtrIndex < 1)
                startAtrIndex = 1;

            for (int k = startAtrIndex;
                k < startAtrIndex + AtrPeriod && k <= index;
                k++)
            {
                avgAtr += TrueRange(bars[k], bars[k - 1]);
            }
            avgAtr /= AtrPeriod;

            for (int m = startAtrIndex + AtrPeriod;
                m <= index;
                m++)
            {
                decimal tr = TrueRange(bars[m], bars[m - 1]);
                avgAtr =
                    ((avgAtr * (AtrPeriod - 1)) + tr) / AtrPeriod;
            }

            result[index] =
                SafeClose(bars[index], out decimal close) && close != 0
                ? avgAtr / close
                : null;
        }
        return result;
    }

    private static bool SafeClose
        (MarketData bar, out decimal close)
    {
        close = bar.Close;
        return close > 0;
    }

    private static decimal TrueRange
        (MarketData current, MarketData prev)
    {
        decimal hL = current.High - current.Low;
        decimal hC = Math.Abs(current.High - prev.Close);
        decimal lC = Math.Abs(current.Low - prev.Close);
        return Math.Max(hL, Math.Max(hC, lC));
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
