using Microsoft.ML;
using QuantTrading.ML.Features;
using QuantTrading.ML.Models;

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
        if (data is null || data.Count == 0)
            throw new InvalidOperationException("No training data available.");

        int totalRows = data.Count;

        // 8:2 split for training and testing
        int trainCount = (int)(totalRows * 0.8);
        var trainRows = data.Take(trainCount).ToList();
        var testRows = data.Skip(trainCount).ToList();

        IDataView trainDataView =
            _mlContext.Data.LoadFromEnumerable(trainRows);
        IDataView testDataView =
            _mlContext.Data.LoadFromEnumerable(testRows);

        var featurePipeline = _mlContext.Transforms.Concatenate(
            "Features", featureColumns);

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

        //var leaderboard = new List<TournamentResult>();
        string winningModelName = "None";
        ITransformer? bestModel = null;
        double highestAuc = 0.0;
        //var trainedArtifacts =
        //    new Dictionary<string, (ITransformer Model, DataViewSchema Schema)>();

        foreach (var algo in algorithms)
        {
            try
            {
                var fullPipeline =
                    featurePipeline.Append(algo.Value);
                ITransformer trainedModel =
                    fullPipeline.Fit(trainDataView);

                IDataView predictions = 
                    trainedModel.Transform(testDataView);
                var metrics = _mlContext.BinaryClassification
                    .Evaluate(predictions, labelColumnName: labelColumn);

                if(metrics.AreaUnderRocCurve > highestAuc)
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

       if (bestModel != null)
        {
            string _bestModelPath = 
                $"{symbol}_{featureName}_best_model.zip";
            _mlContext.Model.Save(
                bestModel,
                trainDataView.Schema,
                _bestModelPath);

            Console.WriteLine($"[SUCCESS] Gold-Medal Model state written to disk at: '{_bestModelPath}'\n");
        }

        return (highestAuc, winningModelName);

    }
}

