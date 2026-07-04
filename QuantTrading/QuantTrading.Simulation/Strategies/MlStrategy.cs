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
    private int _holdDecisions;
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
        if (allocationPerTrade <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(allocationPerTrade),
                "Allocation per trade must be greater than zero.");

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
            _featureGenerator.ComputeTrainingRow(_bars);

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
                (int)CalculatePositionSize(data.Close, accountState.Cash);

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
            _holdDecisions++;
            decision = "HOLD";
            reason = "Already holding position";

            if (_diagnosticMode)
            {
                int heldQuantity =
                    accountState.GetPositionSize(data.Symbol);
                PrintBarDecision(
                    dateStr,
                    data.Close,
                    prediction, $"Long ({heldQuantity})",
                    accountState.Cash,
                    decision,
                    quantity: null,
                    reason);
            }
        }
        else if (!prediction.PredictedLabel && hasPosition)
        {
            _sellSignals++;
            int heldQty =
                accountState.GetPositionSize(data.Symbol);

            if (heldQty > 0)
            {
                order = new OrderRequest(
                    data.Symbol,
                    OrderType.Market,
                    OrderAction.Sell,
                    heldQty);

                _sellOrdersRequested++;
                decision = "SELL";

                if (_diagnosticMode)
                    PrintBarDecision(
                        dateStr,
                        data.Close,
                        prediction,
                        $"Long ({heldQty})",
                        accountState.Cash,
                        decision,
                        quantity: heldQty,
                        reason: null);
            }
            else
            {
                _holdDecisions++;
                decision = "HOLD";
                reason = "Position open but size reported as zero";

                if (_diagnosticMode)
                    PrintBarDecision(
                        dateStr, 
                        data.Close, 
                        prediction, 
                        "Long (0)", 
                        accountState.Cash, 
                        decision, 
                        quantity: null, 
                        reason);
            }
        }
        else
        {
            _holdDecisions++;
            decision = "HOLD";
            reason = "No position to exit";

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

        if (_diagnosticMode && _predictionTable.Count < PredictionTableLimit)
        {
            _predictionTable.Add((
                _predictionsGenerated,
                dateStr,
                data.Close,
                prediction.PredictedLabel,
                prediction.Probability,
                prediction.Score,
                decision));
        }

        return order;
    }

    public void PrintDiagnosticSummary
        (IReadonlyAccountState accountState)
    {
        if (!_diagnosticMode)
            return;

        Console.WriteLine();
        Console.WriteLine("========== ML Strategy Diagnostics ==========");
        Console.WriteLine();
        Console.WriteLine($"Bars Processed           : {_barsProcessed}");
        Console.WriteLine($"Warmup Bars              : {_warmupBars}");
        Console.WriteLine();
        Console.WriteLine($"Predictions Generated    : {_predictionsGenerated}");
        Console.WriteLine();
        Console.WriteLine($"True Predictions         : {_truePredictions}");
        Console.WriteLine($"False Predictions        : {_falsePredictions}");
        Console.WriteLine();
        Console.WriteLine($"Buy Signals              : {_buySignals}");
        Console.WriteLine($"Buy Orders Requested     : {_buyOrdersRequested}");
        Console.WriteLine();
        Console.WriteLine($"Sell Signals             : {_sellSignals}");
        Console.WriteLine($"Sell Orders Requested    : {_sellOrdersRequested}");
        Console.WriteLine();
        Console.WriteLine($"Hold Decisions           : {_holdDecisions}");
        Console.WriteLine($"Rejected Orders          : {_rejectedOrders}");
        Console.WriteLine();
        Console.WriteLine($"Final Cash               : {accountState.Cash:F2}");
        // Note: Final Equity requires BacktestEngine.CalculateCurrentPortfolioValue(strategy).
        // Call that separately after PrintDiagnosticSummary if equity reporting is needed.
        Console.WriteLine();
        Console.WriteLine("=============================================");

        if (_predictionTable.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"First {_predictionTable.Count} Predictions");
            Console.WriteLine();
            Console.WriteLine($"{"#",-4} {"Date",-12} {"Close",-10} {"Prediction",-12} {"Probability",-13} {"Score",-10} {"Action"}");
            Console.WriteLine(new string('-', 68));

            foreach (var (index, date, close, label, probability, score, action) in _predictionTable)
            {
                string predLabel = label ? "Buy" : "Down";
                Console.WriteLine(
                    $"{index,-4} {date,-12} {close,-10:F2} {predLabel,-12} {probability,-13:F2} {score,-10:F2} {action}");
            }

            Console.WriteLine();
        }
    }

    private void PrintBarDecision(
        string date,
        decimal close,
        ModelPrediction prediction,
        string position,
        decimal cash,
        string decision,
        int? quantity,
        string? reason)
    {
        string predLabel = prediction.PredictedLabel ? "Buy (True)" : "Sell/Down (False)";

        Console.WriteLine($"Date       : {date}");
        Console.WriteLine($"Close      : {close:F2}");
        Console.WriteLine();
        Console.WriteLine($"Prediction : {predLabel}");
        Console.WriteLine($"Probability: {prediction.Probability:F2}");
        Console.WriteLine($"Score      : {prediction.Score:F2}");
        Console.WriteLine();
        Console.WriteLine($"Position   : {position}");
        Console.WriteLine($"Cash       : {cash:F2}");
        Console.WriteLine();
        Console.WriteLine($"Decision   : {decision}");

        if (quantity.HasValue)
            Console.WriteLine($"Quantity   : {quantity.Value}");
        if (reason is not null)
            Console.WriteLine($"Reason     : {reason}");

        Console.WriteLine();
        Console.WriteLine("-------------------------");
        Console.WriteLine();
    }

    private int CalculatePositionSize
        (decimal price, decimal availableCash)
    {
        if (price <= 0)
            return 0;
        decimal effectiveAllocation = Math.Min(availableCash, _allocationPerTrade);
        return (int)Math.Floor(effectiveAllocation / price);
    }
}
