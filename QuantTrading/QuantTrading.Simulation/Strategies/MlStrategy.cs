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
    private readonly bool _diagnosticMode;

    private int _barsProcessed, _warmupBars;
    private bool _warmupComplete;

    private int _predictionsGenerated, _truePredictions,
        _falsePredictions;

    private int _buySignals;
    private int _buyOrdersRequested;
    private int _sellSignals;
    private int _sellOrdersRequested;
    private int _holdDescisions;
    private int _rejectedOrders;

    private readonly List<(
        int index, string date, decimal close,
        bool label, float probability, float score,
        string action)> _predictionTable = new();
    private const int PredictionTableLimit = 20;

    public string Name => "ml-directional-model";

    public MlStrategy(
        string modelPath,
        decimal allocationPerTrade = 2000m,
        int maxHistoryBars = 100,
        bool diagnosticMode = false)
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
        _diagnosticMode = diagnosticMode;

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

        _predictionEngine = mlContext.Model
            .CreatePredictionEngine<TrainingRow, ModelPrediction>(model);
        _featureGenerator = new FeatureGenerator();

    }

    public OrderRequest? OnData
        (MarketData data, IReadonlyAccountState accountState)
    {
        if (data is null || data.Close <= 0)
            return null;

        _barsProcessed++;
        _bars.Add(data);

        if (_bars.Count > _maxHistoryBars)
        {
            _bars.RemoveAt(0);
        }

        TrainingRow? latestFeature =
            _featureGenerator.ComputeLatestFeatures(_bars);

        if (latestFeature is null)
        {
            if (!_warmupComplete)
                _warmupBars++;
            return null;
        }

        _warmupComplete = true;

        ModelPrediction prediction;
        try
        {
            prediction = _predictionEngine
                .Predict(latestFeature);
        }
        catch (Exception ex)
        {
            if (_diagnosticMode)
                Console.WriteLine
                    ($"[PREDICTION ERROR] Bar {_barsProcessed} ({data.Timestamp:yyyy-MM-dd}): {ex.Message}");
            return null;
        }

        _predictionsGenerated++;

        if (prediction.PredictedLabel)
            _truePredictions++;
        else
            _falsePredictions++;

        bool hasPosition =
            accountState.HasPositionOpen(data.Symbol);
        string dateStr =
            data.Timestamp.ToString("yyyy-MM-dd");

        OrderRequest? order = null;
        string decision;
        string? reason = null;

        if (prediction.PredictedLabel && !hasPosition)
        {
            _buySignals++;
            int targetShares =
                (int)CalculatePositionSize(data.Close);

            if (targetShares > 0)
            {
                order = new OrderRequest(
                    data.Symbol,
                    OrderType.Market,
                    OrderAction.Buy,
                    targetShares);

                _buyOrdersRequested++;
                decision = "BUY";

                if (_diagnosticMode)
                    PrintBarDecision(
                        dateStr,
                        data.Close,
                        prediction,
                        "Flat",
                        accountState.Cash,
                        decision,
                        targetShares,
                        reason: null);

            }
            else
            {
                _rejectedOrders++;
                decision = "HOLD";
                reason = "Position size calculated to zero";
                if (_diagnosticMode)
                    PrintBarDecision(
                        dateStr,
                        data.Close,
                        prediction,
                        "Flat",
                        accountState.Cash,
                        decision,
                        quantity: null,
                        reason);
            }
        }
        else if (prediction.PredictedLabel && hasPosition)
        {

        }

        return null;
    }

    private void PrintBarDecision(
        string dateStr,
        decimal close,
        ModelPrediction prediction,
        string position,
        decimal cash,
        string decision,
        int? quantity,
        string? reason)
    {
        throw new NotImplementedException();
    }

    private int CalculatePositionSize(decimal price)
    {
        if (price <= 0)
            return 0;
        return (int)Math.Floor(_allocationPerTrade / price);
    }
}
