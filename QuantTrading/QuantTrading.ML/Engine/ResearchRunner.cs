using QuantTrading.Infrastructure.Data;
using QuantTrading.ML.Models;
using QuantTrading.Shared.Features;

namespace QuantTrading.ML.Engine;

public sealed class ResearchRunner
{
    private const string DataFolder = "Data";
    public void RunExperimentPipeline()
    {
        string dataPath =
            Path.Combine(AppContext.BaseDirectory, DataFolder);

        if (!Directory.Exists(dataPath))
        {
            Console.WriteLine($"[INFO] Creating missing Data folder at: {dataPath}");
            Directory.CreateDirectory(dataPath);
            Console.WriteLine("Drop your historical asset CSVs (AAPL.csv, MSFT.csv, etc.) into it and restart.");
            return;
        }

        string[] csvFiles =
            Directory.GetFiles(dataPath, "*.csv");
        if (csvFiles.Length == 0)
        {
            Console.WriteLine($"[INFO] No CSV files found in {dataPath}. Please add your historical asset data and restart.");
            return;
        }

        List<ExperimentResult> researchLedger = new();

        Console.WriteLine("====================================================================================================");
        Console.WriteLine($" 🚀 STARTING MULTI-SYMBOL EVALUATION PIPELINE ({csvFiles.Length} Assets Detected)");
        Console.WriteLine("====================================================================================================");

        LocalCsvParser parser = new();
        FeatureGenerator featureGen = new();
        ModelTrainer modelTrainer = new();

        foreach (var filePath in csvFiles)
        {
            string symbol =
                Path.GetFileNameWithoutExtension(filePath).ToUpper();
            Console.WriteLine($"\n▶️ RUNNING TOURNAMENT EXPERIMENTS FOR: [{symbol}]");

            var marketData = parser.ParseFile(filePath);
            if (marketData.Count == 0)
                continue;

            var trainingData = 
                featureGen.ComputeTrainingRows(marketData);
            if(trainingData.Count == 0)
                continue;

            var upDays =
                trainingData.Count(x => x.IsTomorrowCloseHigher);
            double upRatio =
                (double)upDays / trainingData.Count * 100;
            Console.WriteLine($"   ↳ Data Profile: {trainingData.Count} processed rows | Directional Class Balance: {upRatio:F1}% Up");

            var baseResult =
                modelTrainer.TrainTournament(
                    symbol,
                    trainingData,
                    FeatureSets.BaseFeatures,
                    FeatureSetType.Base.ToString());

            var rsiResult =
                modelTrainer.TrainTournament(
                    symbol,
                    trainingData,
                    FeatureSets.RsiFeatures,
                    FeatureSetType.Rsi.ToString());

            double alphaDelta = 
                rsiResult.Auc - baseResult.Auc;

            researchLedger.Add(new ExperimentResult(
                Symbol: symbol,
                BaseAuc: baseResult.Auc,
                RsiAuc: rsiResult.Auc,
                Delta: alphaDelta,
                BaseWinner: baseResult.ModelName,
                RsiWinner: rsiResult.ModelName));
        }

        ConsoleReport.PrintSummaryLedger(researchLedger);

    }
}
