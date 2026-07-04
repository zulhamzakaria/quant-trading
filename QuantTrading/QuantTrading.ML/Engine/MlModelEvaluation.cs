using Microsoft.ML;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;

namespace QuantTrading.ML.Engine;

public sealed class MlModelEvaluation
{
    public void ExecuteEvaluationPipeline(
        IReadOnlyList<MarketData> bars,
        string modelPath,
        bool isSyntheticData = false)
    {
        if (bars is null || bars.Count < 24)
            throw new ArgumentException(
                "Evaluation requires at least 24 bars (warmup + 1 prediction + 1 ground truth).",
                nameof(bars));

        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException(
                "Model path cannot be empty or null",
                nameof(modelPath));

        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                "Model file was not found",
                modelPath);

        Console.WriteLine("--- Starting ML Model Evaluation ---");
        Console.WriteLine($"Total Bars               : {bars.Count}");
        Console.WriteLine($"Model                    : {modelPath}");

        // Synthetic data warning — printed at runtime so it appears
        // in output, not just as a code comment.
        if (isSyntheticData)
        {
            Console.WriteLine();
            Console.WriteLine("[NOTE] Synthetic data detected.");
            Console.WriteLine("       Accuracy and precision metrics are not");
            Console.WriteLine("       meaningful against a model trained on");
            Console.WriteLine("       real market data. Use actual historical");
            Console.WriteLine("       bars for predictive quality evaluation.");
        }

        Console.WriteLine();

        MLContext mLContext = new(seed: 42);

        ITransformer model;
        try
        {
            model = mLContext.Model.Load(modelPath, out _);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException
                ($"Failed to load model from path: {modelPath}", ex);
        }

        var predictionEngine =
            mLContext.Model
            .CreatePredictionEngine<TrainingRow, ModelPrediction>(model);

        FeatureGenerator featureGenerator = new();

        int totalPredictions = 0;
        int truePositives = 0;
        int falsePositives = 0;
        int trueNegatives = 0;
        int falseNegatives = 0;
        int actualUpDays = 0;
        int actualDownDays = 0;

        List<MarketData> buffer = new(capacity: 100);

        for (int i = 0; i < bars.Count -1; i++)
        {
            buffer.Add(bars[i]);
            if (buffer.Count > 100)
                buffer.RemoveAt(0);

            TrainingRow? features =
                featureGenerator.ComputeTrainingRow(buffer);

            if (features is null)
                continue;

            ModelPrediction prediction;
            try
            {
                prediction = predictionEngine.Predict(features);
            }
            catch (Exception ex)
            {

                Console.WriteLine(
                    $"[PREDICTION ERROR] Bar {i} " +
                    $"({bars[i].Timestamp:yyyy-MM-dd}): {ex.Message}");
                continue;
            }

            // Ground truth: did tomorrow's close exceed today's close?
            int lastIndex = i + 1;
            bool actuallyUp = bars[i+1].Close > bars[i].Close;

            if (actuallyUp)
                actualUpDays++;
            else
                actualDownDays++;

            totalPredictions++;

            if(prediction.PredictedLabel && actuallyUp)
                truePositives++;
            else if(prediction.PredictedLabel && !actuallyUp)
                falsePositives++;
            else if (!prediction.PredictedLabel && !actuallyUp)
                trueNegatives++;
            else
                falseNegatives++;

        }

        int correct = truePositives + trueNegatives;
        int incorrect = falsePositives + falseNegatives;
        int predictedUp = truePositives + falsePositives;
        int predictedDown = trueNegatives + falseNegatives;

        double accuracy = totalPredictions > 0
            ? (double)correct / totalPredictions * 100.0
            : 0.0;

        double precision = predictedUp > 0
            ? (double)truePositives / predictedUp * 100.0
            : 0.0;

        double recall = (truePositives + falseNegatives) > 0
            ? (double)truePositives / (truePositives + falseNegatives) * 100.0
            : 0.0;

        double f1 = (precision + recall) > 0
            ? 2.0 * (precision * recall)/(precision + recall)
            : 0.0;

        // ── Report ────────────────────────────────────────────────────────
        Console.WriteLine("========== ML Model Evaluation ==========");
        Console.WriteLine();

        // Class distribution — makes model bias immediately visible
        // without having to derive it from the confusion matrix manually
        Console.WriteLine("--- Class Distribution ---");
        Console.WriteLine($"Actual Up Days           : {actualUpDays}");
        Console.WriteLine($"Actual Down Days         : {actualDownDays}");
        Console.WriteLine($"Predicted Up             : {predictedUp}");
        Console.WriteLine($"Predicted Down           : {predictedDown}");
        Console.WriteLine();

        Console.WriteLine("--- Metrics ---");
        Console.WriteLine($"Predictions              : {totalPredictions}");
        Console.WriteLine($"Correct                  : {correct}");
        Console.WriteLine($"Incorrect                : {incorrect}");
        Console.WriteLine();
        Console.WriteLine($"Accuracy                 : {accuracy:F2}%");
        Console.WriteLine($"Precision                : {precision:F2}%");
        Console.WriteLine($"Recall                   : {recall:F2}%");
        Console.WriteLine($"F1 Score                 : {f1:F2}%");
        Console.WriteLine();

        Console.WriteLine("--- Confusion Matrix ---");
        Console.WriteLine();
        Console.WriteLine($"{"",20} {"Actual Up",12} {"Actual Down",12}");
        Console.WriteLine($"{"Predicted Buy",20} {truePositives,12} {falsePositives,12}");
        Console.WriteLine($"{"Predicted Down",20} {falseNegatives,12} {trueNegatives,12}");
        Console.WriteLine();
        Console.WriteLine("=========================================");
        Console.WriteLine();

        bool infrastructureIssue = false;

        if(totalPredictions == 0)
        {
            Console.WriteLine(
                "[PIPELINE WARNING] No predictions produced — " +
                "check warmup requirements and bar count.");
            infrastructureIssue = true;
        }

        if (totalPredictions > 0 && predictedUp == totalPredictions)
        {
            Console.WriteLine(
                "[PIPELINE WARNING] Model predicted Buy on every bar — " +
                "inference may be broken.");
            infrastructureIssue = true;
        }

        if (totalPredictions > 0 && predictedDown == totalPredictions)
        {
            Console.WriteLine(
                "[PIPELINE WARNING] Model predicted Down on every bar — " +
                "inference may be broken.");
            infrastructureIssue = true;
        }

        if (!isSyntheticData)
        {
            if (accuracy < 50.0)
                Console.WriteLine(
                    "[MODEL NOTE] Accuracy below 50% — model may be underfit, " +
                    "overfit, or feature drift is present.");

            if (predictedUp > 0 && falsePositives > truePositives)
                Console.WriteLine(
                    "[MODEL NOTE] More false Buy signals than true — " +
                    "precision is low. Review feature alignment with training data.");

            double upBias = totalPredictions > 0
                ? (double)predictedUp / totalPredictions * 100.0
                : 0.0;

            if (upBias > 80.0)
                Console.WriteLine(
                    $"[MODEL NOTE] Model predicts Buy {upBias:F1}% of the time — " +
                    "may be biased toward Up class.");
        }

        if (!infrastructureIssue)
            Console.WriteLine(
                "[SUCCESS] Model evaluation completed. " +
                "Pipeline is functioning correctly.");

    }
}
