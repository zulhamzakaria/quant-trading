using QuantTrading.Infrastructure.Data;
using QuantTrading.Shared.Features;

namespace QuantTrading.ML.Engine.Experiments;

public sealed class PriceZScoreAaplExperiment
{
    public void Run(string aaplCSVPath = "Data/AAPL.csv")
    {
        LocalCsvParser parser = new();
        FeatureGenerator featureGenerator = new();
        ModelTrainer trainer = new();

        var marketData = parser.ParseFile(aaplCSVPath);
        var trainingData =
            featureGenerator.ComputeTrainingRows(marketData);

        Console.WriteLine($"AAPL: {trainingData.Count} training rows computed.");

        var result = trainer.TrainTournament(
            "AAPL",
            trainingData,
            FeatureSets.BaseObvPriceZScoreFeatures,
            FeatureSetType.BaseObvPriceZScore.ToString(), 
            saveModel: false);

        Console.WriteLine($"[RESULT] BaseObvPriceZScore — Best Algorithm: {result.ModelName}, AUC: {result.Auc:F4}");
    }
}
