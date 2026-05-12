using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuantTrading.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
    x => x.MigrationsAssembly("QuantTrading.Infrastructure")
));

// with using, Host disposed when scope ends
using IHost host = builder.Build();

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