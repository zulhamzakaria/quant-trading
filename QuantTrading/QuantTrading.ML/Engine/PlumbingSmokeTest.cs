using QuantTrading.Shared.Models;
using QuantTrading.Simulation.Engine;
using QuantTrading.Simulation.Strategies;

namespace QuantTrading.ML.Engine;

public sealed class PlumbingSmokeTest
{
    public void ExecuteVerificationPipeline()
    {
        // ==========================================
        // 1. GENERATE MOCK HISTORICAL DATA FEED
        // ==========================================
        var historicalData = new List<MarketData>
        {
            // Parameters: Symbol, Timestamp, Open, High, Low, Close, Volume
            new("AAPL", DateTime.UtcNow.AddDays(-4), 149.50m, 151.00m, 149.00m, 150.00m, 1000000m),
            new("AAPL", DateTime.UtcNow.AddDays(-3), 150.20m, 153.00m, 150.00m, 152.50m, 1200000m),
            new("AAPL", DateTime.UtcNow.AddDays(-2), 152.00m, 152.20m, 147.50m, 148.00m, 1500000m), // Strategy triggers BUY (< $149)
            new("AAPL", DateTime.UtcNow.AddDays(-1), 148.50m, 156.00m, 148.00m, 155.00m, 2000000m)  // Strategy triggers SELL (> $154)
        };

        // ==========================================
        // 2. SETUP ORCHESTRATION FRAMEWORK
        // ==========================================
        var engine = new BacktestEngine();
        var mockStrategy = new DummyBuyAndHoldStrategy();

        engine.RegisterStrategy(mockStrategy, startingCash: 10000.00m, currency: "USD");

        Console.WriteLine("--- Starting Simulation Plumbing Verification ---");
        Console.WriteLine($"Initial Account Cash: ${engine.GetAccountState(mockStrategy).Cash:N2}");

        // ==========================================
        // 3. EXECUTE SIMULATION
        // ==========================================
        engine.RunSimulation(historicalData);

        // ==========================================
        // 4. VERIFY INFRASTRUCTURE BALANCES
        // ==========================================
        Console.WriteLine("\n--- Smoke Test Results ---");
        var finalAccountState = engine.GetAccountState(mockStrategy);
        decimal finalTotalValue = engine.CalculateCurrentPortfolioValue(mockStrategy);

        Console.WriteLine($"Ending Cash Balance:  ${finalAccountState.Cash:N2} (Expected: $10,070.00)");
        Console.WriteLine($"Ending Total Equity:  ${finalTotalValue:N2} (Expected: $10,070.00)");

        int remainingPositionsCount = finalAccountState.ActivePositions.Count;
        Console.WriteLine($"Active Positions Open: {remainingPositionsCount} (Expected: 0)");

        foreach (var position in finalAccountState.ActivePositions)
        {
            Console.WriteLine($"-> Leftover Position: {position.Key} | Shares: {position.Value}");
        }

        // Automated validation gate
        if (finalAccountState.Cash == 10070.00m && remainingPositionsCount == 0)
        {
            Console.WriteLine("\n[SUCCESS] Backtest engine plumbing behaves perfectly. Core framework verified.");
        }
        else
        {
            Console.WriteLine("\n[FAILURE] Mathematical mismatch detected in plumbing layer calculation loop.");
        }
    }
}
