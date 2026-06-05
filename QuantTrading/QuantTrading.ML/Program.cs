using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuantTrading.Domain.Models;
using QuantTrading.Domain.ValueObjects;
using QuantTrading.Infrastructure;
using QuantTrading.Simulation.Engine;
using QuantTrading.Simulation.Models;
using QuantTrading.Simulation.Strategies;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
    x => x.MigrationsAssembly("QuantTrading.Infrastructure")
));

// with using, Host disposed when scope ends
using IHost host = builder.Build();

using IServiceScope scope = host.Services.CreateScope();

//using (var scope = host.Services.CreateScope())
//{
//    var services = scope.ServiceProvider;
//    var engine = services.GetRequiredService<BacktestService>();

//    // Fixed dates for reproducibility
//    var start = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);
//    var end = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

//    try
//    {   // use IOption<> for this?
//        await engine.RunAsync("AAPL", start, end);
//    }
//    catch (Exception ex)
//    {
//        var logger = services.GetRequiredService<ILogger<Program>>();
//        logger.LogError(ex, "Unhandled exception during backtest.");
//        Console.WriteLine($"Critical Error: {ex.Message}");
//    }
//}

// Mock Run
// 1. Arrange: Create exactly 3 mock chronological data bars for AAPL
var mockHistoricalData = new List<MarketData>
{
    new MarketData(Symbol:"AAPL", Timestamp: new DateTime(2026, 1, 1), Open: 99m, High: 102m, Low: 98m, Close: 100m, Volume: 1500000m),
    new MarketData(Symbol:"AAPL", Timestamp: new DateTime(2026, 1, 2), Open: 101m, High: 112m, Low: 100m, Close: 110m, Volume: 2000000m),
    new MarketData(Symbol:"AAPL", Timestamp: new DateTime(2026, 1, 3), Open: 109m, High: 110m, Low: 94m, Close: 95m, Volume: 1800000m)
};

// 2. Arrange: Initialize components with $10,000 USD initial capital
var initialCapital = new Money(10000m, Currency.USD);
var engine = new BacktestEngine();
var strategy = new DummyBuyAndHoldStrategy();

// 3. Act: Run the calculation loop
BacktestRunResult result = engine.RunSimulation(strategy, mockHistoricalData, initialCapital);

// 4. Assert: Print output to verify mathematical accuracy
Console.WriteLine("\n=== BACKTEST RESULTS SUMMARY ===");
Console.WriteLine($"Strategy:        {result.StrategyName}");
Console.WriteLine($"Timeline:        {result.EquityCurve.First().Timestamp:yyyy-MM-dd} to {result.EquityCurve.Last().Timestamp:yyyy-MM-dd}");
Console.WriteLine($"Initial Capital: {result.InitialCapital.Amount} {result.InitialCapital.Currency}");
Console.WriteLine($"Final Capital:   {result.FinalPortfolioValue.Amount} {result.FinalPortfolioValue.Currency}");
Console.WriteLine($"Total Return Percentage:   {result.TotalReturnPercentage}%");
Console.WriteLine($"Total Fills:     {result.Fills.Count}");

Console.WriteLine("\n--- Historical Equity Curve Checkpoint ---");
foreach (var point in result.EquityCurve)
{
    Console.WriteLine($"[{point.Timestamp:yyyy-MM-dd}] Portfolio Value: ${point.Value.Amount}");
}
Console.WriteLine("==========================================");