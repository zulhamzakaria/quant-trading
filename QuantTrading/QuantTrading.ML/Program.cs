using QuantTrading.Infrastructure.Data;
using QuantTrading.Shared.Contracts;
using QuantTrading.Simulation.Reporting;
using QuantTrading.Simulation.Strategies;
using QuantTrading.Simulation.Tournament;

class Program
{
    private const string CsvPath = "AAPL.csv";
    private const string ModelPath = "AAPL_Base_best_model.zip";
    private const decimal StartingCapital = 10_000m;

    // Checkpoint 2 — Confidence Thresholding experiment: COMPLETE, REJECTED.
    // See handoff doc. Entries at default (0.5, unfiltered) confirmed best.

    // Checkpoint 2 — Probability-Based Exit experiment (current).
    // Entries left at default (unfiltered) to isolate the exit variable.
    // 0.50 omitted: it's a no-op (identical to the original label-flip-only
    // exit), already covered by the Checkpoint 1 baseline.
    private static readonly float[] ExitThresholds =
        {0.55f, 0.60f, 0.65f, 0.70f};
    static void Main(string[] args)
    {
        LocalCsvParser parser = new();
        var historicalData = parser.ParseFile(CsvPath);

        Console.WriteLine($"Parsed {historicalData.Count} " +
            $"rows of historical data from {CsvPath}.");

        TournamentRunner runner = new(StartingCapital);
        TournamentReporter reporter = new();

        foreach (float threshold in ExitThresholds)
        {
            Console.WriteLine();
            Console.WriteLine(new string('#', 60));
            Console.WriteLine($"# ML EXIT THRESHOLD = {threshold:F2}");
            Console.WriteLine(new string('#', 60));

            var strats = new List<IStrategy>
            {
                new BuyAndHoldStrategy(),
                new MaCrossStrategy(),
                new RsiStrategy(),
                new BollingerBandsStrategy(),
                new MlStrategy(ModelPath, exitThreshold: threshold)
            };

            var results = runner.Run(strats, historicalData);
            reporter.PrintReport(results);
        }
    }
}