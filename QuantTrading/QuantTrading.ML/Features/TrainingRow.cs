namespace QuantTrading.ML.Features;

public sealed record TrainingRow(
    //DateTime Timestamp,
    float Return1D,
    float Return5D,
    float Sma5Ratio,
    float Sma20Ratio,
    float VolumeRatio,
    float Rsi14,
    float AtrRatio14,
    bool IsTomorrowCloseHigher
);

