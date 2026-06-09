using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuantTrading.Infrastructure;
using QuantTrading.Infrastructure.Data;
using QuantTrading.ML.Features;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
    x => x.MigrationsAssembly("QuantTrading.Infrastructure")
));

// with using, Host disposed when scope ends
using IHost host = builder.Build();

using IServiceScope scope = host.Services.CreateScope();

var csvPath = builder.Configuration["TrainingDataPath"]
    ?? throw new InvalidOperationException(
        "TrainingDataPath is not configured.");

// Checkpoint 1: Load and parse historical market data from CSV file
var parser = new LocalCsvParser();
var marketData = parser.ParseFile(csvPath);

if (marketData.Count == 0)
{
    Console.WriteLine($"No valid market data found in {csvPath}. Please check the file format and contents.");
}
else
{
    Console.WriteLine($"Loaded {marketData.Count} bars of historical data for {marketData.First().Symbol} from {csvPath}.");
}

// Checkpoint 2: Feature engineering and data preprocessing (placeholder for actual implementation)
FeatureGenerator featureGenerator = new();
var trainingData = featureGenerator.ComputeFeatures(marketData);
Console.WriteLine($"[SUCCESS] Transformed raw matrices into {trainingData.Count} feature rows!");

if (trainingData.Count > 0)
{
    var sample = trainingData[0];
    Console.WriteLine($"Sample Row [Date: {sample.Timestamp:dd-MM-yyyy}] -> Return1D: " +
        $"{sample.Return1D:F4}, Sma20Ratio: {sample.Sma20Ratio:F4}, VolumeRatio: {sample.VolumeRatio:F4}");
}

// Checkpoint 3
// 1. ChatGPT's Class Balance Check
var upDays = trainingData.Count(x => x.IsTomorrowCloseHigher);
var downDays = trainingData.Count - upDays;
double upRatio = (double)upDays / trainingData.Count * 100;

Console.WriteLine($"--- DATASET CLASS BALANCE ---");
Console.WriteLine($"Total Rows: {trainingData.Count}");
Console.WriteLine($"Up Days   (True) : {upDays} ({upRatio:F1}%)");
Console.WriteLine($"Down Days (False): {downDays} ({(100 - upRatio):F1}%)");
Console.WriteLine($"-----------------------------");

if (trainingData.Count > 0)
{
    var sample = trainingData[0];
    // Notice how we can still read sample.Timestamp if you keep it in the record!
    Console.WriteLine($"Sample Row [Date: {sample.Timestamp:yyyy-MM-dd}] -> Return1D: {sample.Return1D:F4}, LABEL: {sample.IsTomorrowCloseHigher}");
}