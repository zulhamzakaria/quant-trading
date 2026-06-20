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

    public BacktestRunResult RunSimulation(
        IStrategy strategy, IEnumerable<MarketData> historicalData, Money initialCapital)
    {
        throw new NotImplementedException();
    }
}
