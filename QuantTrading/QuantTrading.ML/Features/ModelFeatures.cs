namespace QuantTrading.ML.Features;

public sealed record ModelFeatures(
    DateTime Timestamp,
    float Return1D,
    float Return5D,
    float Sma5Ratio,
    float Sma20Ratio,
    float VolumeRatio
);

