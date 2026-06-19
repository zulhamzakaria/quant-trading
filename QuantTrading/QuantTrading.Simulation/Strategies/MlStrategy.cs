using Microsoft.ML;
using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;

namespace QuantTrading.Simulation.Strategies;

public sealed class MlStrategy : IStrategy
{
    private readonly PredictionEngine<TrainingRow, ModelPrediction>
        _predictionEngine;
    private readonly FeatureGenerator featureGenerator;
    private readonly List<MarketData> _bars = new();

    private readonly decimal _allocationPerTrade;
    private readonly int _maxHistoryBars;

    public string Name => "ml-directional-model";

    public MlStrategy(
        string modelPath, 
        decimal allocationPerTrade = 2000m,
        int maxHistoryBars = 100)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException(
                "Model path cannot be empty or null",
                nameof(modelPath));
        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                "Target ML model binary file was not found",
                modelPath);
    }

    public OrderRequest? OnData(MarketData data, IReadonlyAccountState accountState)
    {
        throw new NotImplementedException();
    }
}
