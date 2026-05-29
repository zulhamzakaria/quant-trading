using QuantTrading.Application.Models;
using QuantTrading.Domain.Models;
using QuantTrading.Domain.ValueObjects;
using QuantTrading.Simulation.Contracts;

namespace QuantTrading.Simulation.Engine;

public sealed class BacktestEngine
{
    public List<EquityCurvePoint> RunSimulation(
        IStrategy strategy, IEnumerable<MarketData> marketData, Money initialCapital)
    {
        Console.WriteLine($"[Engine] Initializing simulation loop for strategy: {strategy.Name}");



    }
}
