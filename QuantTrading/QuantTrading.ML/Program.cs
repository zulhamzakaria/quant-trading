using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuantTrading.Infrastructure;
using QuantTrading.Infrastructure.Data;

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
