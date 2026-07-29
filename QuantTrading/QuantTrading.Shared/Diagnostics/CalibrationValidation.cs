using Microsoft.ML;
using Microsoft.ML.Trainers;
using QuantTrading.Shared.Models;

namespace QuantTrading.Shared.Diagnostics;

// One-off validation script — NOT part of the production feature pipeline.
// Isolates two effects that a naive 80/20 -> 70/10/20 split change would
// otherwise conflate: (1) does shrinking training data alone move the
// tournament champion, independent of calibration; (2) does fitting Platt
// on a genuinely held-out slice improve calibration quality without
// materially harming ranking (AUC). Transform -> Fit(calibrator) -> Transform
// sequence verified against ML.NET's official sample and a maintainer's own
// GitHub answer (dotnet/machinelearning#6120) before being used here.
public static class CalibrationValidation
{
    private const string LabelColumn =
        nameof(TrainingRow.IsTomorrowCloseHigher);

    public static void RunComparison(
        string symbol,
        IReadOnlyList<TrainingRow> data,
        string[] featureColumns,
        string featureName)
    {
        MLContext mlContext = new(seed: 42);

        Console.WriteLine("========== Calibration Investigation ==========");
        Console.WriteLine($"Symbol: {symbol}, FeatureSet: {featureName}, Rows: {data.Count}");
        Console.WriteLine();

        // Experiment 1: current 80/20 baseline — confirms today's numbers.
        RunSplit(mlContext, "80/20 (current baseline)", data, featureColumns,
            trainFrac: 0.80, calibrateFrac: 0.0, testFrac: 0.20);

        // Experiment 2: 70/30 control, NO calibration slice. Isolates
        // whether shrinking training data alone (independent of
        // calibration) already changes the champion or AUC.
        RunSplit(mlContext, "70/30 (control — no calibration slice)", data, featureColumns,
            trainFrac: 0.70, calibrateFrac: 0.0, testFrac: 0.30);

        // Experiment 3: 70/10/20 with genuine held-out calibration.
        RunSplit(mlContext, "70/10/20 (held-out calibration)", data, featureColumns,
            trainFrac: 0.70, calibrateFrac: 0.10, testFrac: 0.20,
            reportCalibrationMetrics: true);

        Console.WriteLine();
        Console.WriteLine("Directional objectives (pre-registered, not numeric thresholds):");
        Console.WriteLine("  - Brier Score should improve (lower) under held-out calibration.");
        Console.WriteLine("  - Reliability curve should move closer to the diagonal.");
        Console.WriteLine("  - AUC (ranking performance) should not materially degrade.");
        Console.WriteLine("  (Whether AUC actually holds is reported below, not assumed.)");
        Console.WriteLine("=================================================");


    }

    private static void RunSplit(
        MLContext mlContext,
        string label,
        IReadOnlyList<TrainingRow> data,
        string[] featureColumns,
        double trainFrac,
        double calibrateFrac,
        double testFrac,
        bool reportCalibrationMetrics = false)
    {
        Console.WriteLine($"--- {label} ---");

        int totalRows = data.Count;
        int trainCount = (int)(totalRows * trainFrac);
        int calibrateCount = (int)(totalRows * calibrateFrac);

        // Strictly chronological split — no shuffling, same convention as
        // the existing 80/20 split (data.Take/data.Skip). No look-ahead risk.
        var trainRows = data.Take(trainCount).ToList();
        var calibrateRows =
            calibrateCount > 0
            ? data.Skip(trainCount).Take(calibrateCount).ToList()
            : null;
        var testRows =
            data.Skip(trainCount + calibrateCount).ToList();

        IDataView trainDataView =
            mlContext.Data.LoadFromEnumerable(trainRows);
        IDataView testDataView =
            mlContext.Data.LoadFromEnumerable(testRows);

        var featurePipeline =
            mlContext.Transforms.Concatenate("Features", featureColumns);

        // SDCA determinism fix already confirmed (see handoff doc) — applied
        // here too, not re-verified, per "don't re-litigate a closed
        // investigation" unless this step introduces new stochastic behavior.
        var sdcaOptions = new SdcaLogisticRegressionBinaryTrainer.Options
        {
            LabelColumnName = LabelColumn,
            FeatureColumnName = "Features",
            Shuffle = false,
            NumberOfThreads = 1,
        };

        var algorithms = new Dictionary<string, IEstimator<ITransformer>>
        {
            {"SDCA Logistic Regression (Linear)", mlContext.BinaryClassification.Trainers
                .SdcaLogisticRegression(sdcaOptions)},
            {"L-BFGS Logistic Regression (Linear)", mlContext.BinaryClassification.Trainers
                .LbfgsLogisticRegression(LabelColumn, "Features")},
            {"Fast Tree (Gradient Boosted)", mlContext.BinaryClassification.Trainers
                .FastTree(LabelColumn, "Features")},   // uncalibrated here — Platt fit separately below
            {"Fast Forest (Random Forest Ensemble)", mlContext.BinaryClassification.Trainers
                .FastForest(LabelColumn, "Features")},
        };

        string winningModelName = "None";
        ITransformer? bestUncalibrated = null;
        double highestAuc = 0.0;

        foreach (var algo in algorithms)
        {
            var fullPipeline =
                featurePipeline.Append(algo.Value);
            ITransformer trainedModel =
                fullPipeline.Fit(trainDataView);
            IDataView predictions =
                trainedModel.Transform(testDataView);
            var metrics =
                mlContext.BinaryClassification.Evaluate(
                    predictions,
                    labelColumnName: LabelColumn);

            Console.WriteLine($"  [ALGO] {algo.Key,-40} AUC: {metrics.AreaUnderRocCurve:F4}");

            if (metrics.AreaUnderRocCurve > highestAuc)
            {
                highestAuc = metrics.AreaUnderRocCurve;
                winningModelName = algo.Key;
                bestUncalibrated = trainedModel;
            }
        }

        Console.WriteLine($"  Winner: {winningModelName} (pre-calibration AUC: {highestAuc:F4})");

        if (reportCalibrationMetrics && calibrateRows is not null && bestUncalibrated is not null)
        {
            // Verified sequence (matches ML.NET's official sample and
            // dotnet/machinelearning#6120): Transform() to produce Score,
            // then Fit(calibrator) on that scored data. ITransformer has
            // no .Append() — chain via sequential .Transform() calls, not
            // pipeline composition, since bestUncalibrated is already fit.
            IDataView calibrateDataView = mlContext.Data.LoadFromEnumerable(calibrateRows);
            IDataView scoredCalibData = bestUncalibrated.Transform(calibrateDataView);
            var calibrator = mlContext.BinaryClassification.Calibrators.Platt(LabelColumn).Fit(scoredCalibData);

            IDataView scoredTestData = bestUncalibrated.Transform(testDataView);
            IDataView calibratedTestData = calibrator.Transform(scoredTestData);

            var postCalibMetrics = mlContext.BinaryClassification.Evaluate(
                calibratedTestData, labelColumnName: LabelColumn);

            // Reported, not assumed — whether AUC actually holds after
            // calibration is exactly what this experiment is checking.
            Console.WriteLine($"  Post-calibration AUC: {postCalibMetrics.AreaUnderRocCurve:F4} " +
                $"(pre-calibration was {highestAuc:F4})");
            Console.WriteLine($"  Brier Score (post-calibration): {ComputeBrierScore(mlContext, calibratedTestData):F4}");

            // Pre-calibration Brier too, for a genuine before/after comparison —
            // uses raw Score run through a simple sigmoid-free proxy isn't
            // valid, so instead we report pre-calibration AUC as the ranking
            // baseline and Brier only where Probability is meaningfully defined
            // (i.e. post-calibration). Pre-calibration probability quality for
            // FastTree/FastForest is exactly what's broken today (see
            // ModelTrainer's Platt-on-training-data defect) — not reported
            // here as a second Brier number to avoid comparing calibrated
            // Brier against an already-known-broken baseline.
            PrintReliabilityDiagram(mlContext, calibratedTestData);
        }

        Console.WriteLine();

    }

    private static double ComputeBrierScore(MLContext mlContext, IDataView predictions)
    {
        var rows = mlContext.Data.CreateEnumerable<CalibrationPredictionRow>(predictions, reuseRowObject: false).ToList();
        if (rows.Count == 0) return double.NaN;

        double sumSquaredError = rows.Sum(r => Math.Pow(r.Probability - (r.Label ? 1.0 : 0.0), 2));
        return sumSquaredError / rows.Count;
    }

    // 5 bins, not deciles — a ~200-250 row calibration/test slice would
    // make deciles (~20-25 samples/bin) too thin to read reliably.
    private static void PrintReliabilityDiagram(MLContext mlContext, IDataView predictions)
    {
        var rows = mlContext.Data.CreateEnumerable<CalibrationPredictionRow>(predictions, reuseRowObject: false).ToList();
        if (rows.Count == 0)
        {
            Console.WriteLine("  Reliability diagram: no rows to bin.");
            return;
        }

        Console.WriteLine("  Reliability diagram (5 bins):");
        Console.WriteLine($"    {"Bin Range",-15}{"N",-6}{"Avg Predicted",-16}{"Observed Freq",-16}{"Gap"}");

        for (int bin = 0; bin < 5; bin++)
        {
            double lower = bin * 0.2;
            double upper = (bin + 1) * 0.2;
            var inBin = rows.Where(r => r.Probability >= lower && (bin == 4 ? r.Probability <= upper : r.Probability < upper)).ToList();

            if (inBin.Count == 0)
            {
                Console.WriteLine($"    [{lower:F1}-{upper:F1})     0     -               -               -");
                continue;
            }

            double avgPredicted = inBin.Average(r => r.Probability);
            double observedFreq = inBin.Count(r => r.Label) / (double)inBin.Count;
            Console.WriteLine($"    [{lower:F1}-{upper:F1})     {inBin.Count,-6}{avgPredicted,-16:F4}{observedFreq,-16:F4}{Math.Abs(avgPredicted - observedFreq):F4}");
        }
    }

    private sealed class CalibrationPredictionRow
    {
        [Microsoft.ML.Data.ColumnName(nameof(TrainingRow.IsTomorrowCloseHigher))]
        public bool Label { get; set; }

        [Microsoft.ML.Data.ColumnName("Probability")]
        public float Probability { get; set; }
    }

}
