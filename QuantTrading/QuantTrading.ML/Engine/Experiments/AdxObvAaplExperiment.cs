using QuantTrading.Infrastructure.Data;
using QuantTrading.Shared.Features;

namespace QuantTrading.ML.Engine.Experiments;

public sealed class AdxObvAaplExperiment
{
    public void Run(string aaplCsvPath = "Data/AAPL.csv")
    {
        LocalCsvParser parser = new();
        FeatureGenerator featureGen = new();
        ModelTrainer trainer = new();

        var marketData = parser.ParseFile(aaplCsvPath);
        var trainingData =
            featureGen.ComputeTrainingRows(marketData);

        Console.WriteLine($"AAPL: {trainingData.Count} training rows computed.");

        var result = trainer.TrainTournament(
            "AAPL",
            trainingData,
            FeatureSets.BaseAdxObvFeatures,
            FeatureSetType.BaseAdxObv.ToString(),
            saveModel: false);

        Console.WriteLine($"[RESULT] BaseAdxObv — Best Algorithm: {result.ModelName}, AUC: {result.Auc:F4}");

    }
}
