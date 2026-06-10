using Microsoft.ML;
using QuantTrading.ML.Features;

namespace QuantTrading.ML.Engine;

public sealed class ModelTrainer
{
    private readonly MLContext _mlContext;
    private readonly string _modelPath = "model.zip";
    public ModelTrainer()
    {
        _mlContext = new MLContext(seed: 42);
    }

    public void TrainAndEvaluate(IReadOnlyList<TrainingRow> data)
    {
        if (data is null || data.Count == 0)
            throw new InvalidOperationException("No training data available.");

        int totalRows = data.Count;
        int historicalUpDays =
            data.Count(x => x.IsTomorrowCloseHigher);
        double baselineAccuracy =
            (double)historicalUpDays / totalRows;

        // 8:2 split for training and testing
        int trainCount = (int)(totalRows * 0.8);
        var trainRows = data.Take(trainCount).ToList();
        var testRows = data.Skip(trainCount).ToList();

        IDataView trainDataView = 
            _mlContext.Data.LoadFromEnumerable(trainRows);
        IDataView testDataView = 
            _mlContext.Data.LoadFromEnumerable(testRows);

        var pipeline = _mlContext.Transforms.Concatenate(
            "Features",
            nameof(TrainingRow.Return1D),
            nameof(TrainingRow.Return5D),
            nameof(TrainingRow.Sma5Ratio),
            nameof(TrainingRow.Sma20Ratio),
            nameof(TrainingRow.VolumeRatio))
            .Append(_mlContext.BinaryClassification
            .Trainers.SdcaLogisticRegression(
                labelColumnName: nameof(TrainingRow.IsTomorrowCloseHigher),
                featureColumnName: "Features"));

        Console.WriteLine("Training the model...");
        ITransformer trainedModel = 
            pipeline.Fit(trainDataView);
        Console.WriteLine("[SUCCESS] Initial training completed.");

        IDataView predictions = 
            trainedModel.Transform(testDataView);
        var metrics = _mlContext.BinaryClassification.Evaluate(
            predictions, labelColumnName: nameof(TrainingRow.IsTomorrowCloseHigher));

        Console.WriteLine("\n==================================================");
        Console.WriteLine("--- SYSTEM PERFORMANCE EVALUATION REPORT ---");
        Console.WriteLine($"Naive Baseline Accuracy: {baselineAccuracy:P2} (Guessing UP every day)");
        Console.WriteLine($"Machine Model Accuracy : {metrics.Accuracy:P2} (Unseen Data Performance)");
        Console.WriteLine($"AUC Score             : {metrics.AreaUnderRocCurve:F4} (0.50 = Coin Flip)");
        Console.WriteLine($"F1 Score              : {metrics.F1Score:F4}");
        Console.WriteLine("==================================================");

        _mlContext.Model.Save(
            trainedModel, trainDataView.Schema, _modelPath);
        Console.WriteLine($"[SUCCESS] Trained model saved to: {_modelPath}");
    }
}
