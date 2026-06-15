using QuantTrading.ML.Engine;

//// Db service not being used

//var builder = Host.CreateApplicationBuilder(args);
//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
//    x => x.MigrationsAssembly("QuantTrading.Infrastructure")
//));
//// with using, Host disposed when scope ends
//using IHost host = builder.Build();
//using IServiceScope scope = host.Services.CreateScope();

class Program
{
    static void Main(string[] args)
    {
        ResearchRunner runner = new();
        runner.RunExperimentPipeline();
    }
}