using QuantTrading.Infrastructure.Data;
using QuantTrading.Shared.Features;

namespace QuantTrading.ML.Engine.Experiments;

public sealed class ObvAaplExperiment
{
    public void Run(string aapCsvPath = "Data/AAPL.csv")
    {
        LocalCsvParser parser = new();
        FeatureGenerator featureGenerator = new();
        ModelTrainer trainer = new();

        var marketData = parser.ParseFile(aapCsvPath);
        var trainingData = 
            featureGenerator.ComputeTrainingRows(marketData);

        Console.WriteLine($"AAPL: {trainingData.Count} training rows computed.");

        var result = trainer.TrainTournament(
            "AAPL",
            trainingData,
            FeatureSets.BaseObvFeatures,
            FeatureSetType.BaseObv.ToString(), 
            saveModel: false);

        Console.WriteLine($"[RESULT] BaseObv — Best Algorithm: {result.ModelName}, AUC: {result.Auc:F4}");
    }
}
