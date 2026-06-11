using Microsoft.ML;
using QuantTrading.ML.Features;
using QuantTrading.ML.Models;

namespace QuantTrading.ML.Engine;

public sealed class ModelTrainer
{
    private readonly MLContext _mlContext;
    private readonly string _bestModelPath = "best_model.zip";
    public ModelTrainer()
    {
        _mlContext = new MLContext(seed: 42);
    }

    public void TrainAndEvaluate(IReadOnlyList<TrainingRow> data)
    {
        if (data is null || data.Count == 0)
            throw new InvalidOperationException("No training data available.");

        int totalRows = data.Count;
        int historicalUpDays =
            data.Count(x => x.IsTomorrowCloseHigher);
        double baselineAccuracy =
            (double)historicalUpDays / totalRows;

        // 8:2 split for training and testing
        int trainCount = (int)(totalRows * 0.8);
        var trainRows = data.Take(trainCount).ToList();
        var testRows = data.Skip(trainCount).ToList();

        IDataView trainDataView =
            _mlContext.Data.LoadFromEnumerable(trainRows);
        IDataView testDataView =
            _mlContext.Data.LoadFromEnumerable(testRows);

        var featurePipeline = _mlContext.Transforms.Concatenate(
            "Features",
            nameof(TrainingRow.Return1D),
            nameof(TrainingRow.Return5D),
            nameof(TrainingRow.Sma5Ratio),
            nameof(TrainingRow.Sma20Ratio),
            nameof(TrainingRow.VolumeRatio));

        string labelColumn =
            nameof(TrainingRow.IsTomorrowCloseHigher);

        var algorithms = new Dictionary<string, IEstimator<ITransformer>>
        {
            {"SDCA Logistic Regression (Linear)", _mlContext
            .BinaryClassification.Trainers
            .SdcaLogisticRegression(labelColumn, "Features")},
            {"L-BFGS Logistic Regression (Linear)", _mlContext
            .BinaryClassification.Trainers
            .LbfgsLogisticRegression(labelColumn, "Features")},
            {"Fast Tree (Gradient Boosted)", _mlContext
            .BinaryClassification.Trainers
            .FastTree(labelColumn, "Features")
            .Append(_mlContext.BinaryClassification.Calibrators
            .Platt(labelColumn))},
            {"Fast Forest (Random Forest Ensemble)", _mlContext
            .BinaryClassification.Trainers
            .FastForest(labelColumn, "Features")
            .Append(_mlContext.BinaryClassification.Calibrators
            .Platt(labelColumn))}
        };

        var leaderboard = new List<TournamentResult>();
        ITransformer bestModel = null;
        double highestAuc = 0.0;
        var trainedArtifacts =
            new Dictionary<string, (ITransformer Model, DataViewSchema Schema)>();

        Console.WriteLine($"\n======================================================================");
        Console.WriteLine($"🏆 STARTING THE MODEL TOURNAMENT ({algorithms.Count} Contenders) 🏆");
        Console.WriteLine($"Chronological Training Rows: {trainRows.Count} | Testing Rows: {testRows.Count}");
        Console.WriteLine($"======================================================================\n");

        foreach (var algo in algorithms)
        {
            try
            {
                Console.WriteLine($"[TRAINING] Fitting {algo.Key}...");
                var fullPipeline =
                    featurePipeline.Append(algo.Value);
                ITransformer trainedModel =
                    fullPipeline.Fit(trainDataView);

                IDataView predictions =
                    trainedModel.Transform(testDataView);
                var metrics =
                    _mlContext.BinaryClassification
                    .Evaluate(predictions, labelColumn);
                leaderboard.Add(new TournamentResult(
                    algo.Key,
                    metrics.Accuracy,
                    metrics.AreaUnderRocCurve,
                    metrics.F1Score));

                if (metrics.AreaUnderRocCurve > highestAuc)
                {
                    highestAuc = metrics.AreaUnderRocCurve;
                    bestModel = trainedModel;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Training {algo.Key} failed: {ex.Message}");
                continue;
            }
        }

        Console.WriteLine("\n======================================================================");
        Console.WriteLine("                    🏆 FINAL TOURNAMENT LEADERBOARD 🏆");
        Console.WriteLine($" Naive Market Baseline Accuracy: {baselineAccuracy:P2}");
        Console.WriteLine("======================================================================");
        Console.WriteLine(string.Format("{0,-38} | {1,-9} | {2,-8} | {3,-7}", "Algorithm Model", "Accuracy", "AUC", "F1 Score"));
        Console.WriteLine("----------------------------------------------------------------------");

        foreach (var result in leaderboard.OrderByDescending(r => r.AUC))
        {
            Console.WriteLine(string.Format("{0,-38} | {1,-9:P2} | {2,-8:F4} | {3,-7:F4}",
                result.Name, result.Accuracy, result.AUC, result.F1Score));
        }

        Console.WriteLine("======================================================================");
        if (bestModel != null)
        {
            _mlContext.Model.Save(
                bestModel,
                trainDataView.Schema,
                _bestModelPath);

            Console.WriteLine($"[SUCCESS] Gold-Medal Model state written to disk at: '{_bestModelPath}'\n");
        }
    }
}

