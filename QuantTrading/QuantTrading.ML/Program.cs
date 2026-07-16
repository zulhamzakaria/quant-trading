using QuantTrading.Infrastructure.Data;
using QuantTrading.ML.Engine;
using QuantTrading.ML.Engine.Experiments;
using QuantTrading.Shared.Contracts;
using QuantTrading.Simulation.Reporting;
using QuantTrading.Simulation.Strategies;
using QuantTrading.Simulation.Tournament;

class Program
{
    private const string CsvPath = "AAPL.csv";
    //private const string ModelPath = "AAPL_Rsi_best_model.zip";
    private const string DefaultModelPath = "AAPL_Base_best_model.zip";
    private const decimal StartingCapital = 10_000m;

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
            default:
                PrintUsage();
                break;
        }
    }

    private static void RunSimulation(string modelPath)
    {
        LocalCsvParser parser = new();
        var historicalData = parser.ParseFile(CsvPath);

        Console.WriteLine($"Parsed {historicalData.Count} " +
            $"rows of historical data from {CsvPath}.");
        Console.WriteLine($"Using ML model: {modelPath}");

        TournamentRunner runner = new(StartingCapital);
        TournamentReporter reporter = new();

        var strats = new List<IStrategy>
        {
            new BuyAndHoldStrategy(),
            new MaCrossStrategy(),
            new RsiStrategy(),
            new BollingerBandsStrategy(),
            new MlStrategy(DefaultModelPath)
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

    }

}