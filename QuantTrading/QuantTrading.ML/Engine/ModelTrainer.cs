using Microsoft.ML;
using Microsoft.ML.Trainers;
using QuantTrading.Shared.Models;

namespace QuantTrading.ML.Engine;

public sealed class ModelTrainer
{
    private readonly MLContext _mlContext;
    public ModelTrainer()
    {
        _mlContext = new MLContext(seed: 42);
    }

    public (double Auc, string ModelName) TrainTournament(
        string symbol,
        IReadOnlyCollection<TrainingRow> data,
        string[] featureColumns,
        string featureName)
    {
        var result = TrainTournament(symbol, data, featureColumns, featureName, saveModel: true);
        return (result.Auc, result.ModelName);
    }

    public TrainedModelResult TrainTournament(
    string symbol,
    IReadOnlyCollection<TrainingRow> data,
    string[] featureColumns,
    string featureName,
    bool saveModel)
    {
        if (data is null || data.Count == 0)
            throw new InvalidOperationException("No training data available.");

        int totalRows = data.Count;
        int trainCount = (int)(totalRows * 0.8);
        var trainRows = data.Take(trainCount).ToList();
        var testRows = data.Skip(trainCount).ToList();

        IDataView trainDataView = _mlContext.Data.LoadFromEnumerable(trainRows);
        IDataView testDataView = _mlContext.Data.LoadFromEnumerable(testRows);

        var featurePipeline = _mlContext.Transforms.Concatenate("Features", featureColumns);
        string labelColumn = nameof(TrainingRow.IsTomorrowCloseHigher);

        var options = new SdcaLogisticRegressionBinaryTrainer.Options
        {
            LabelColumnName = labelColumn,
            FeatureColumnName = "Features",
            Shuffle = false,          // disable epoch shuffling
            NumberOfThreads = 1,      // single-threaded for reproducibility
            ConvergenceTolerance = 0.01F // optional: tighter tolerance
        };

        var algorithms = new Dictionary<string, IEstimator<ITransformer>>
        {
            //{"SDCA Logistic Regression (Linear)", _mlContext.BinaryClassification.Trainers
            //    .SdcaLogisticRegression(labelColumn, "Features")},
            {"SDCA Logistic Regression (Linear)", _mlContext.BinaryClassification.Trainers
                .SdcaLogisticRegression(options)},
            {"L-BFGS Logistic Regression (Linear)", _mlContext.BinaryClassification.Trainers
                .LbfgsLogisticRegression(labelColumn, "Features")},
            {"Fast Tree (Gradient Boosted)", _mlContext.BinaryClassification.Trainers
                .FastTree(labelColumn, "Features")
                .Append(_mlContext.BinaryClassification.Calibrators.Platt(labelColumn))},
            {"Fast Forest (Random Forest Ensemble)", _mlContext.BinaryClassification.Trainers
                .FastForest(labelColumn, "Features")
                .Append(_mlContext.BinaryClassification.Calibrators.Platt(labelColumn))}
        };

        string winningModelName = "None";
        ITransformer? bestModel = null;
        double highestAuc = 0.0;


        foreach (var algo in algorithms)
        {
            try
            {
                var fullPipeline = featurePipeline.Append(algo.Value);
                ITransformer trainedModel = fullPipeline.Fit(trainDataView);
                IDataView predictions = trainedModel.Transform(testDataView);
                var metrics = _mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: labelColumn);

                // New — log every algorithm's AUC, not just whichever wins.
                // Purely additive diagnostic logging, no behavior change. Needed
                // to separate "is a specific algorithm unstable across runs" from
                // "did a different algorithm just happen to win this time" —
                // the winner-only view can't distinguish these.
                Console.WriteLine($"  [ALGO] {algo.Key,-40} " +
                    $"AUC: {metrics.AreaUnderRocCurve:F4}, " +
                    $"ACC: {metrics.Accuracy:F4}, " +
                    $"+ PREC: {metrics.PositivePrecision:F4} " +
                    $"+ RECL: {metrics.PositiveRecall:F4} " +
                    $"FI: {metrics.F1Score:F4}");

                if (metrics.AreaUnderRocCurve > highestAuc)
                {
                    highestAuc = metrics.AreaUnderRocCurve;
                    winningModelName = algo.Key;
                    bestModel = trainedModel;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Training {algo.Key} failed: {ex.Message}");
                continue;
            }
        }

        if (bestModel is null)
            throw new InvalidOperationException(
                $"No algorithm successfully trained for symbol '{symbol}', feature set '{featureName}'. " +
                "All candidate algorithms failed — check the [ERROR] lines above for individual failures.");

        if (saveModel)
        {
            string bestModelPath = Path.Combine(AppContext.BaseDirectory, $"{symbol}_{featureName}_best_model.zip");
            _mlContext.Model.Save(bestModel, trainDataView.Schema, bestModelPath);
            Console.WriteLine($"[SUCCESS] Gold-Medal Model state written to disk at: '{bestModelPath}'\n");
        }

        // bestModel and trainDataView.Schema are guaranteed non-null here —
        // the throw above already ruled out the null case.
        return new TrainedModelResult(highestAuc, winningModelName, bestModel, trainDataView.Schema, symbol, featureName);
    }
}

