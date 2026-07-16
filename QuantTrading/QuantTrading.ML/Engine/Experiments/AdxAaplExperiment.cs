using QuantTrading.Infrastructure.Data;
using QuantTrading.Shared.Features;

namespace QuantTrading.ML.Engine.Experiments;

// Checkpoint 2 — Feature Engineering (ADX).
// Strictly AAPL per methodology: isolates ADX against frozen AAPL baseline,
// same as Feature Selection. Uses the same pipeline components (LocalCsvParser,
// FeatureGenerator, ModelTrainer) as ResearchRunner, but scoped to one symbol/
// feature set instead of looping the full Data folder. ResearchRunner remains
// untouched; Phase 5 can extend it to run BaseAdxFeatures across all symbols
// without refactor

public sealed class AdxAaplExperiment
{
    public void Run(string aaplCsvPath = "Data/AAPL.csv")
    {
        LocalCsvParser parser = new();
        FeatureGenerator featureGenerator = new();
        ModelTrainer modelTrainer = new();

        var marketData =
            parser.ParseFile(aaplCsvPath);
        var trainingData = 
            featureGenerator.ComputeTrainingRows(marketData);

        Console.WriteLine($"AAPL: {trainingData.Count} training rows computed.");

        var result = modelTrainer.TrainTournament(
            "AAPL",
            trainingData,
            FeatureSets.BaseAdxFeatures,
            FeatureSetType.BaseAdx.ToString());

        Console.WriteLine($"[RESULT] BaseAdx — Best Algorithm: {result.ModelName}, AUC: {result.Auc:F4}");

    }
}
