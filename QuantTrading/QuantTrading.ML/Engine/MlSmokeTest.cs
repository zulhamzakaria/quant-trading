
using QuantTrading.Shared.Models;
using QuantTrading.Simulation.Engine;
using QuantTrading.Simulation.Strategies;

namespace QuantTrading.ML.Engine;

public sealed class MlSmokeTest
{
    public void ExecuteVerificationPipeline()
    {
        // ==========================================
        // 1. LOAD HISTORICAL TEST DATA
        // ==========================================
        var historicalData = GenerateHistoricalData();

        // ==========================================
        // 2. SETUP STRATEGY + ENGINE
        // ==========================================
        var strategy = new MlStrategy(
            modelPath: "AAPL_Base_best_model.zip");

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
        var finalState =
            engine.GetAccountState(strategy);

        decimal finalPortfolioValue =
            engine.CalculateCurrentPortfolioValue(
                strategy);

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
    }

    private static List<MarketData> GenerateHistoricalData()
    {
        var data = new List<MarketData>();

        decimal price = 100m;

        for (int i = 0; i < 50; i++)
        {
            price += 0.75m;

            data.Add(
                new MarketData(
                    Symbol: "AAPL",
                    Timestamp: DateTime.UtcNow.AddDays(-50 + i),
                    Open: price - 0.50m,
                    High: price + 1.00m,
                    Low: price - 1.00m,
                    Close: price,
                    Volume: 1_000_000m));
        }

        return data;
    }
}
