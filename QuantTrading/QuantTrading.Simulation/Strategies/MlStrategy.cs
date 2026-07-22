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

    private readonly decimal? _allocationPerTrade;
    private readonly decimal? _equityAllocationPct;
    private readonly bool _diagnosticMode;

    private readonly float _confidenceThreshold;

    // Probability-based early exit threshold (Checkpoint 2, exit-strategy
    // experiment). Independent of _confidenceThreshold — entry and exit are
    // different trading decisions and are not coupled.
    // Semantics: exit if Probability < _exitThreshold while holding, even
    // if PredictedLabel is still nominally true. This only has a meaningful
    // effect when _exitThreshold > 0.5 — e.g. 0.60 means "exit if
    // conviction in the up-move has weakened below 60%, even though the
    // model technically still predicts up." A threshold at or below 0.5
    // is a no-op: Probability < 0.5 already coincides with PredictedLabel
    // == false in the normal case, making it redundant with the existing
    // label-flip exit. Default of 0.5f therefore disables early exits,
    // preserving prior behavior exactly.
    // Nullable by design: null disables early-exit entirely.
    // Earlier default (float 0.5) assumed Probability < 0.5 == PredictedLabel false.
    // That assumption was wrong — they disagree in ~1–2% of cases (e.g. Prob=0.474, Label=true).
    // Result: silent early exits. Nullable removes this class of bug by eliminating any default cutoff.
    private readonly float? _exitThreshold;

    private int _barsProcessed;
    private int _predictionsGenerated, _truePredictions,
    _falsePredictions;

    private int _buySignals;
    private int _buyOrdersRequested;
    private int _sellSignals;
    private int _sellOrdersRequested;
    private int _holdDecisions;
    private int _rejectedOrders;
    private int _lowConfidenceSkips;
    private int _earlyExitsOnWeakConfidence;

    private readonly List<(
        int index, string date, decimal close,
        bool label, float probability, float score,
        string action)> _predictionTable = new();
    private const int PredictionTableLimit = 20;

    public string Name => "ml-directional-model";

    public MlStrategy(
        string modelPath,
        decimal? allocationPerTrade = 2000m,
        bool diagnosticMode = false,
        float confidenceThreshold = 0.5f,
        float? exitThreshold = null,
        decimal? equityAllocationPct = null)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException(
                "Model path cannot be empty or null",
                nameof(modelPath));
        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                "Target ML model binary file was not found",
                modelPath);
        int sizingModesSet =
            (allocationPerTrade is not null ? 1 : 0) +
            (equityAllocationPct is not null ? 1 : 0);
        if (sizingModesSet != 1)
            throw new ArgumentException(
                "Exactly one sizing mode must be specified: allocationPerTrade " +
                "(fixed dollar) or equityAllocationPct (percent of equity) — not both, not neither.");
        if (allocationPerTrade is { } dollar && dollar <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(allocationPerTrade),
                "Allocation per trade must be greater than zero.");
        if (equityAllocationPct is { } pct && (pct <= 0 || pct > 1))
            throw new ArgumentOutOfRangeException(
                nameof(equityAllocationPct),
                "Equity allocation percent must be in the range (0, 1].");
        if (confidenceThreshold < 0.5f || confidenceThreshold >= 1f)
            throw new ArgumentOutOfRangeException(
                nameof(confidenceThreshold),
                "Confidence threshold must be in the range [0.5, 1.0) — " +
                "0.5 means no filtering; values below 0.5 or at/above 1.0 are not meaningful.");
        if (exitThreshold is not null &&
            (exitThreshold < 0.5f || exitThreshold >= 1f))
            throw new ArgumentOutOfRangeException(
                nameof(exitThreshold),
                "Exit threshold must be in the range [0.5, 1.0) — " +
                "0.5 disables early exits (prior behavior, no-op since Probability < 0.5 " +
                "already coincides with PredictedLabel == false); values above 0.5 " +
                "tighten the hold requirement (exit while confidence weakens, even if " +
                "still nominally predicting up).");

        _allocationPerTrade = allocationPerTrade;
        _equityAllocationPct = equityAllocationPct;
        _diagnosticMode = diagnosticMode;
        _confidenceThreshold = confidenceThreshold;
        _exitThreshold = exitThreshold;

        // Initialize ML.NET Context with set evaluation seeds
        var mlContext = new MLContext(seed: 42);

        ITransformer model;
        try
        {
            model = mlContext.Model.Load(modelPath, out _);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException
                ($"Failed to load or parse the ML model from path: {modelPath}", ex);
        }

        _predictionEngine = mlContext.Model
            .CreatePredictionEngine<TrainingRow, ModelPrediction>(model);

    }

    public OrderRequest? OnData(
        MarketData data,
        MarketFeatures features,
        IReadonlyAccountState accountState)
    {
        if (data is null || data.Close <= 0)
            return null;

        _barsProcessed++;

        TrainingRow? latestFeature =
            TrainingRow.FromMarketFeatures(features);

        ModelPrediction prediction;
        try
        {
            prediction =
                _predictionEngine.Predict(latestFeature);
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

        // Confidence thresholding (Checkpoint 2, entry-filtering):
        // Probability = P(PredictedLabel == true), consistent across trainers.
        // Scope: entries only. Buys require confidentUp;
        // exits remain raw PredictedLabel to isolate entry effect.
        // Probability-aware exits are a later experiment.
        // At threshold 0.5, confidentUp == PredictedLabel (no filtering).
        bool confidentUp =
            prediction.Probability >= _confidenceThreshold;
        bool confidentDown =
            prediction.Probability <= (1 - _confidenceThreshold);

        bool hasPosition =
            accountState.HasPositionOpen(data.Symbol);
        string dateStr =
            data.Timestamp.ToString("yyyy-MM-dd");

        OrderRequest? order = null;
        string decision;
        string? reason = null;

        // Exit condition (Checkpoint 2, probability-based exit experiment):
        // supplements, does not replace, the original label-flip exit.
        // Exits fire on PredictedLabel == false (unchanged, original logic)
        // OR when held-position confidence weakens below _exitThreshold,
        // even if PredictedLabel hasn't technically flipped yet. Checked
        // before the "already holding" branch so an early exit can fire
        // whenever confidence has dropped below the (raised) bar, even
        // while PredictedLabel is still nominally true. At the default
        // exitThreshold of 0.5f this condition is a no-op — Probability < 0.5
        // already coincides with PredictedLabel == false in the normal case
        // — so behavior is identical to the original label-flip-only exit.
        bool shouldExitOnWeakConfidence = hasPosition &&
            _exitThreshold is { } threshold &&
            prediction.Probability < threshold;

        if (confidentUp && !hasPosition)
        {
            _buySignals++;

            if (_equityAllocationPct is { } pct)
            {
                order = new OrderRequest(
                    data.Symbol,
                    OrderType.Market,
                    OrderAction.Buy,
                    new SizingInstruction.EquityFraction(pct));

                _buyOrdersRequested++;
                decision = "BUY";

                if (_diagnosticMode)
                    PrintBarDecision(dateStr, data.Close, prediction, "Flat",
                        accountState.Cash, decision, quantity: null,
                        reason: $"Equity-fraction sizing ({pct:P0}); shares computed at execution");
            }
            else
            {
                int targetShares = (int)CalculatePositionSize(
                    data.Close,
                    accountState.Cash);

                if (targetShares > 0)
                {
                    order = new OrderRequest(
                        data.Symbol,
                        OrderType.Market,
                        OrderAction.Buy,
                        new SizingInstruction.FixedQuantity(targetShares));

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
        }
        else if
            ((!prediction.PredictedLabel || shouldExitOnWeakConfidence)
            && hasPosition)
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
                    new SizingInstruction.FixedQuantity(heldQty));

                _sellOrdersRequested++;
                decision = "SELL";

                if (shouldExitOnWeakConfidence
                    && prediction.PredictedLabel)
                {
                    _earlyExitsOnWeakConfidence++;
                    // TEMP DEBUG — remove after confirming the early-exit
                    // path actually fires for the chosen exitThreshold.
                    if (_earlyExitsOnWeakConfidence <= 5)
                        Console.WriteLine(
                            $"[DEBUG] Early exit #{_earlyExitsOnWeakConfidence} on {dateStr} — " +
                            $"Probability={prediction.Probability:F3} < exitThreshold, " +
                            $"PredictedLabel still true.");
                }

                var exitReason = shouldExitOnWeakConfidence
                    && prediction.PredictedLabel
                    ? $"Early exit on weak confidence (Probability={prediction.Probability:F2} < exitThreshold={_exitThreshold:F2})"
                    : null;

                if (_diagnosticMode)
                    PrintBarDecision(
                        dateStr,
                        data.Close,
                        prediction,
                        $"Long ({heldQty})",
                        accountState.Cash,
                        decision,
                        quantity: heldQty,
                        reason: exitReason);
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
        else
        {
            _holdDecisions++;
            decision = "HOLD";
            reason = "No position to exit";

            if (!confidentUp && !confidentDown)
            {
                _lowConfidenceSkips++;
                reason = $"Prediction confidence below threshold ({prediction.Probability:F2})";
            }
            else
            {
                reason = "No position to exit";
            }

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
        Console.WriteLine($"Low Confidence Skips     : {_lowConfidenceSkips}");
        Console.WriteLine($"Early Exits (weak conf.) : {_earlyExitsOnWeakConfidence}");
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
            Console.WriteLine($"{"#",-4} " +
                $"{"Date",-12} " +
                $"{"Close",-10} " +
                $"{"Prediction",-12} " +
                $"{"Probability",-13} " +
                $"{"Score",-10} " +
                $"{"Action"}");
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
        string predLabel = prediction.PredictedLabel
            ? "Buy (True)"
            : "Sell/Down (False)";

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
        decimal effectiveAllocation = Math.Min(
            availableCash, 
            _allocationPerTrade.Value);
        return (int)Math.Floor(effectiveAllocation / price);
    }
}
