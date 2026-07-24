using QuantTrading.Infrastructure.Data;
using QuantTrading.ML.Engine;
using QuantTrading.ML.Engine.Experiments;
using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Diagnostics;
using QuantTrading.Simulation.Reporting;
using QuantTrading.Simulation.Strategies;
using QuantTrading.Simulation.Tournament;

class Program
{
    private const string CsvPath = "AAPL.csv";
    //private const string ModelPath = "AAPL_Rsi_best_model.zip";
    private const string DefaultModelPath = "AAPL_Base_best_model.zip";
    private const decimal StartingCapital = 10_000m;

    private const decimal AtrExperimentBaseFraction = 0.20m;
    private const decimal AtrExperimentK = 29.9043m;   // derived: halves at AAPL's 90th-percentile ATR14

    static void Main(string[] args)
    {
        string mode = args.Length > 0
                    ? args[0].ToLowerInvariant()
                    : "";

        switch (mode)
        {
            case "simulate":
                string modelPath = args.Length > 1
                    ? args[1]
                    : DefaultModelPath;
                RunSimulation(modelPath);
                break;
            case "train-all":
                new ResearchRunner().RunExperimentPipeline();
                break;
            case "train-adx-aapl":
                new AdxAaplExperiment().Run();
                break;
            case "train-obv-aapl":
                new ObvAaplExperiment().Run();
                break;
            case "train-adx-obv-aapl":
                new AdxObvAaplExperiment().Run();
                break;
            case "train-pricezscore-aapl":
                new PriceZScoreAaplExperiment().Run();
                break;
            case "validate-atr":
                string csvPath = Path.Combine(AppContext.BaseDirectory, CsvPath);
                var bars = new LocalCsvParser().ParseFile(csvPath);
                AtrValidation.RunComparison(bars);
                break;
            case "validate-rsi":
                string rsiCsvPath = Path.Combine(AppContext.BaseDirectory, CsvPath);
                var rsiBars = new LocalCsvParser().ParseFile(rsiCsvPath);
                RsiValidation.RunComparison(rsiBars);
                break;
            default:
                PrintUsage();
                break;
        }
    }

    private static void RunSimulation(string modelPath)
    {
        string resolvedCsvPath =
            Path.Combine(AppContext.BaseDirectory, CsvPath);
        string resolvedModelPath =
            Path.Combine(AppContext.BaseDirectory, modelPath);

        LocalCsvParser parser = new();
        var historicalData = parser.ParseFile(resolvedCsvPath);

        Console.WriteLine($"Parsed {historicalData.Count} " +
            $"rows of historical data from {CsvPath}.");
        Console.WriteLine($"Using ML model: {modelPath}");

        TournamentRunner runner = new(StartingCapital);
        TournamentReporter reporter = new();

        var strats = new List<IStrategy>
        {
            //new BuyAndHoldStrategy(),
            //new MaCrossStrategy(),
            //new RsiStrategy(),
            //new BollingerBandsStrategy(),
            new MlStrategy(
                resolvedModelPath,
                allocationPerTrade: 2000m,
                equityAllocationPct: null,
                name: "ml-baseline-v2-2000"),
            new MlStrategy(
                resolvedModelPath,
                allocationPerTrade: null,
                atrBaseFraction: AtrExperimentBaseFraction,
                atrK: AtrExperimentK,
                name: "ml-atr-scaled-base20-k29_9")
        };

        var results = runner.Run(strats, historicalData);
        reporter.PrintReport(results);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run -- <mode> [args]");
        Console.WriteLine();
        Console.WriteLine("Modes:");
        Console.WriteLine("  simulate [modelPath]   Run the 5-strategy tournament.");
        Console.WriteLine($"                         modelPath defaults to '{DefaultModelPath}' if omitted.");
        Console.WriteLine("  train-all              Run ResearchRunner's full multi-symbol Base/Rsi pipeline.");
        Console.WriteLine("  train-adx-aapl         Train the AAPL BaseAdx feature set (Checkpoint 2 experiment).");
        Console.WriteLine("  train-obv-aapl         Train the AAPL BaseObv feature set (Checkpoint 2 experiment).");
        Console.WriteLine("  train-adx-obv-aapl     Train the AAPL BaseAdxObv combined feature set (Checkpoint 2).");
        Console.WriteLine("  train-pricezscore-aapl Train AAPL BaseObv+PriceZScore20 vs. the Base+OBV champion.");
    }

}