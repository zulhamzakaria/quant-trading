using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Models;

namespace QuantTrading.Simulation.Strategies;

public sealed class MlStrategy : IStrategy
{
    //private readonly PredictionEngine<TrainingRow, ModelPrediction>
    public string Name => throw new NotImplementedException();

    public OrderRequest? OnData(MarketData data, IReadonlyAccountState accountState)
    {
        throw new NotImplementedException();
    }
}
