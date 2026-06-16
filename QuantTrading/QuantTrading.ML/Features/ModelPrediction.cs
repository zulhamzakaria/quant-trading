using Microsoft.ML.Data;

namespace QuantTrading.ML.Features;

public sealed class ModelPrediction
{
    [ColumnName("PredictedLabel")]public bool PredictedLabel { get; set; }
    [ColumnName("Probability")]public float Probability { get; set; }
    [ColumnName("Score")]public float Score { get; set; }
}
