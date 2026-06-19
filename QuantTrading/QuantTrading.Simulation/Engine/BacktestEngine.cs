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
    public BacktestRunResult RunSimulation(
        IStrategy strategy, IEnumerable<MarketData> historicalData, Money initialCapital)
    {
        Console.WriteLine($"[Engine] Initializing simulation loop for strategy: {strategy.Name}");

        var broker = new SimulatedBroker(initialCapital);
        var equityCurve = new List<EquityCurvePoint>();

        foreach(var bar in historicalData)
        {
            broker.UpdateMarketPrice(bar.Symbol, bar.Close);
            IReadonlyAccountState stateSnapshot = broker.GetCurrentState();

            OrderRequest? orderIntent = strategy.OnData(bar, stateSnapshot);

            if(orderIntent is not null)
            {
                ExecutionResult result = broker.ProcessOrder(orderIntent, bar.Timestamp);

                if (result.IsSuccess)
                {
                    Console.WriteLine($"[{bar.Timestamp:yyyy-MM-dd}] FILLED {orderIntent.Action}: {orderIntent.Quantity} {orderIntent.Symbol} @ {bar.Close}");
                }
                else
                {
                    Console.WriteLine($"[{bar.Timestamp:yyyy-MM-dd}] REJECTED {orderIntent.Action} for {orderIntent.Symbol}: {result.RejectionReason}");
                }
            }
            decimal currentEquity = broker.CalculateTotalPortfolioValue();
            equityCurve.Add(new EquityCurvePoint(bar.Timestamp, new Money(currentEquity, initialCapital.Currency)));
        }
        Console.WriteLine($"[Engine] Backtest run complete. Final Net Wealth: {broker.CalculateTotalPortfolioValue()}");
        return new BacktestRunResult(
            StrategyName: strategy.Name,
            InitialCapital: initialCapital,
            FinalPortfolioValue: new Money(broker.CalculateTotalPortfolioValue(), initialCapital.Currency),
            EquityCurve: equityCurve.AsReadOnly(),
            Fills: broker.GetHistory()
        );
    }
}
