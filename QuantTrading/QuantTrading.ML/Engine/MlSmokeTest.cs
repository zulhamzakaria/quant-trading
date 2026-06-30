
using QuantTrading.Infrastructure.Data;
using QuantTrading.Shared.Models;
using QuantTrading.Simulation.Engine;
using QuantTrading.Simulation.Strategies;

namespace QuantTrading.ML.Engine;

public sealed class MlSmokeTest
{
    private const string CsvPath = "AAPL.csv";
    private const string ModelPath = "AAPL_Base_best_model.zip";

    public void ExecuteVerificationPipeline()
    {
        // ==========================================
        // 1. LOAD HISTORICAL TEST DATA
        // ==========================================
        var parser = new LocalCsvParser();
        var historicalData = parser.ParseFile(CsvPath);
        //var historicalData = GenerateHistoricalData();

        // ==========================================
        // 2. SETUP STRATEGY + ENGINE
        // ==========================================
        var strategy = new MlStrategy(
            modelPath: ModelPath, 
            diagnosticMode: true);

        var engine = new BacktestEngine();

        engine.RegisterStrategy(
            strategy,
            startingCash: 10_000m,
            currency: "USD");

        Console.WriteLine(
            "--- Starting ML Strategy Smoke Test ---");

        Console.WriteLine(
            $"Initial Account Cash: " +
            $"{engine.GetAccountState(strategy).Cash:N2}");

        // ==========================================
        // 3. EXECUTE BACKTEST
        // ==========================================
        engine.RunSimulation(historicalData);

        // ==========================================
        // 4. REPORT RESULTS
        // ==========================================
        var finalState = engine.GetAccountState(strategy);
        strategy.PrintDiagnosticSummary(finalState);

        decimal finalPortfolioValue =
            engine.CalculateCurrentPortfolioValue(strategy);

        Console.WriteLine();
        Console.WriteLine("--- Smoke Test Results ---");

        Console.WriteLine(
            $"Ending Cash Balance: " +
            $"{finalState.Cash:N2}");

        Console.WriteLine(
            $"Ending Portfolio Value: " +
            $"{finalPortfolioValue:N2}");

        Console.WriteLine(
            $"Open Positions: " +
            $"{finalState.ActivePositions.Count}");

        foreach (var position in finalState.ActivePositions)
        {
            Console.WriteLine(
                $"Position: {position.Key} " +
                $"Qty: {position.Value}");
        }

        Console.WriteLine();
        Console.WriteLine(
            "[SUCCESS] ML strategy executed without runtime failures.");

        // ==========================================
        // 6. PHASE 2 — MODEL EVALUATION
        // Tests model prediction quality directly,
        // independent of MlStrategy and BacktestEngine.
        // ==========================================
        Console.WriteLine();
        Console.WriteLine("==========================================");
        Console.WriteLine();

        MlModelEvaluation evaluation = new();
        evaluation.ExecuteEvaluationPipeline(
            historicalData,
            modelPath: ModelPath,
            isSyntheticData: true);
    
    }

    private static List<MarketData> GenerateHistoricalData()
    {
        var data = new List<MarketData>();
        var rng = new Random(Seed: 42);

        decimal price = 100m;

        // Predefined movement pattern to ensure both up and down days
        // without relying on Random alone — guarantees RSI exercises
        // both gain and loss accumulators within the first 22 bars.
        decimal[] movements =
        [
             0.80m, -0.40m,  1.20m, -0.60m,  0.50m,
            -0.30m,  1.50m, -0.80m,  0.20m, -0.50m,
             1.00m, -0.70m,  0.90m, -0.40m,  0.60m,
            -0.20m,  0.75m, -0.55m,  1.10m, -0.35m,
             0.45m, -0.65m,  0.85m, -0.45m,  0.55m
        ];

        for (int i = 0; i < 150; i++)
        {
            // Use predefined pattern for first 25 bars, random thereafter
            decimal change = i < movements.Length
                ? movements[i]
                : Math.Round(
                    (decimal)(rng.NextDouble() * 3.0 - 1.2),
                    2);

            price = Math.Max(price + change, 1m);

            decimal open = price - Math.Round((decimal)(rng.NextDouble() * 0.5), 2);
            decimal high = price + Math.Round((decimal)(rng.NextDouble() * 1.5 + 0.5), 2);
            decimal low = price - Math.Round((decimal)(rng.NextDouble() * 1.5 + 0.5), 2);
            decimal volume = 800_000m + (decimal)(rng.NextDouble() * 400_000);

            // Enforce OHLC integrity
            high = Math.Max(high, Math.Max(open, price));
            low = Math.Min(low, Math.Min(open, price));

            data.Add(
                new MarketData(
                    Symbol: "AAPL",
                    Timestamp: DateTime.UtcNow.AddDays(-150 + i),
                    Open: open,
                    High: high,
                    Low: low,
                    Close: price,
                    Volume: volume));
        }

        return data;
    }
}
