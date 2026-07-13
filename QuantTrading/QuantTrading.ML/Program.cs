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

    //private static readonly float[] confidenceThresholds =
    //    {0.55f, 0.60f, 0.65f, 0.70f, 0.75f};
    static void Main(string[] args)
    {

        LocalCsvParser parser = new();
        var historicalData = parser.ParseFile(CsvPath);

        Console.WriteLine($"Parsed {historicalData.Count} " +
            $"rows of historical data from {CsvPath}.");

        TournamentRunner runner = new(StartingCapital);
        TournamentReporter reporter = new();

        //foreach (float threshold in confidenceThresholds)
        //{
        //    Console.WriteLine();
        //    Console.WriteLine(new string('#', 60));
        //    Console.WriteLine($"# ML CONFIDENCE THRESHOLD = {threshold:F2}");
        //    Console.WriteLine(new string('#', 60));

        var strats = new List<IStrategy>
            {
                new BuyAndHoldStrategy(),
                new MaCrossStrategy(),
        //        new RsiStrategy(),
        //        new BollingerBandsStrategy(),
                new MlStrategy(ModelPath)
            };

        var results = runner.Run(strats, historicalData);
        reporter.PrintReport(results);
        //}
    }
}