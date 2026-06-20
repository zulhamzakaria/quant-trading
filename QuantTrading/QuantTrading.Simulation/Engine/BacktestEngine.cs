using QuantTrading.Domain.Models;
using QuantTrading.Domain.ValueObjects;
using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Models;
using QuantTrading.Simulation.Execution;
using QuantTrading.Simulation.Models;

namespace QuantTrading.Simulation.Engine;

public sealed class BacktestEngine
{
    private readonly List<IStrategy> _strategies = new();
    private readonly Dictionary<IStrategy, StrategyAccountState>
        _strategyAccounts = new();
    private readonly Dictionary<string, decimal>
        _latestPrices = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterStrategy(
        IStrategy strategy,
        decimal startingCash,
        string currency = "USD")
    {
        if (strategy is null)
            throw new ArgumentNullException(nameof(strategy));
        if (startingCash <= 0)
            throw new ArgumentOutOfRangeException
                (nameof(startingCash), "Starting cash balance must be greater than zero.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException
                ("Currency cannot be null, empty, or whitespace.", nameof(currency));
        if (_strategyAccounts.ContainsKey(strategy))
            throw new InvalidOperationException
                ($"The strategy instance '{strategy.Name}' is already registered.");

        _strategies.Add(strategy);
        _strategyAccounts[strategy] =
            new StrategyAccountState(startingCash, currency);
    }

    public BacktestRunResult RunSimulation
        (IEnumerable<MarketData> historicalFeed)
    {
        if (historicalFeed is null)
            throw new ArgumentNullException
                (nameof(historicalFeed));
        if (_strategies.Count == 0)
            throw new InvalidOperationException
                ("Cannot run simulation. No strategies registered");

        foreach(var bar in historicalFeed)
        {
            if (bar.Close <= 0)
                continue;
            _latestPrices[bar.Symbol] = bar.Close;

            for(int i = 0; i < _strategies.Count; i++)
            {
                var strategy = _strategies[i];
                var account = _strategyAccounts[strategy];

                OrderRequest? request = null;
                try
                {
                    request =
                        strategy.OnData(bar, account);
                }
                catch (Exception ex) 
                {
                    Console.WriteLine($"[STRATEGY CRASH] '{strategy.Name}' failed on {bar.Timestamp}: {ex.Message}");
                    continue;
                }

                if (request is not null)
                    ExecuteOrder(
                        request,
                        account,
                        bar.Close,
                        _latestPrices);
            }

        }

    }

    private void ExecuteOrder(
        OrderRequest request, 
        StrategyAccountState account, 
        decimal executionPrice, 
        Dictionary<string, decimal> globalPrices)
    {
        if (request.Shares <= 0 || executionPrice <= 0)
            return;

        globalPrices[request.Symbol] = executionPrice;

        decimal totalValue = request.
    }
}
