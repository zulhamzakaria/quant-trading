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
    static void Main(string[] args)
    {

        LocalCsvParser parser = new();
        var historicalData = parser.ParseFile(CsvPath);

        Console.WriteLine($"Parsed {historicalData.Count} " +
            $"rows of historical data from {CsvPath}.");

        var strats = new List<IStrategy>
        {
            new BuyAndHoldStrategy(),
            new MaCrossStrategy(),
            new RsiStrategy(),
            new BollingerBandsStrategy(),
            new MlStrategy(ModelPath)
        };

        TournamentRunner runner = new(StartingCapital);
        var results = runner.Run(strats, historicalData);

        TournamentReporter reporter = new();
        reporter.PrintReport(results);
    }
}