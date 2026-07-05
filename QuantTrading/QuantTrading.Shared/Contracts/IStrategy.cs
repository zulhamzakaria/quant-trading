using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;

namespace QuantTrading.Shared.Contracts;

public interface IStrategy
{
    string Name { get; }
    OrderRequest? OnData(
        MarketData data,
        MarketFeatures features,
        IReadonlyAccountState accountState);
}
