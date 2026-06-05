using QuantTrading.Domain.Models;
using QuantTrading.Simulation.Models;

namespace QuantTrading.Simulation.Contracts;

public interface IStrategy
{
    string Name { get; }
    OrderRequest? OnData(MarketData data, IReadonlyAccountState accountState);
}
