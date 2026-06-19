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
    private readonly FeatureGenerator _featureGenerator;
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

        if (maxHistoryBars < 25)
            throw new ArgumentOutOfRangeException(
                nameof(maxHistoryBars),
                "Historical lookback window must look back at least 25 bars.");

        _allocationPerTrade = allocationPerTrade;
        _maxHistoryBars = maxHistoryBars;

        // Initialize ML.NET Context with set evaluation seeds
        var mlContext = new MLContext(seed: 42);

        ITransformer model;
        try
        {
            model = mlContext.Model.Load(modelPath, out _);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load or parse the ML model from path: {modelPath}", ex);
        }

        _predictionEngine = mlContext.Model.CreatePredictionEngine<TrainingRow, ModelPrediction>(model);
        _featureGenerator = new FeatureGenerator();

    }

    public OrderRequest? OnData
        (MarketData data, IReadonlyAccountState accountState)
    {
        if (data is null || data.Close <= 0)
            return null;

        _bars.Add(data);

        if (_bars.Count > _maxHistoryBars)
            _bars.RemoveAt(0);

        TrainingRow? latestFeature =
            _featureGenerator.ComputeLatestFeatures(_bars);

        if (latestFeature is null)
            return null;

        ModelPrediction prediction;

        try
        {
            prediction = _predictionEngine.Predict(latestFeature);
        }
        catch
        {
            return null;
        }

        bool hasPosition =
            accountState.HasPositionOpen(data.Symbol);

        if (prediction.PredictedLabel && !hasPosition)
        {
            int targetShares = (int)CalculatePositionSize(data.Close);
            if (targetShares > 0)
                return new OrderRequest(
                    data.Symbol,
                    OrderType.Market,
                    OrderAction.Buy,
                    targetShares);
        }

        return null;
    }

    private int CalculatePositionSize(decimal price)
    {
        if(price <= 0)
            return 0;
        return (int)Math.Floor(_allocationPerTrade / price);
    }
}
