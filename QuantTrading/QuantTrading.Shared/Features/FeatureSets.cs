using QuantTrading.Shared.Models;

namespace QuantTrading.Shared.Features;

public static class FeatureSets
{
    public static readonly string[] BaseFeatures =
    {
        nameof(TrainingRow.Return1D),
        nameof(TrainingRow.Return5D),
        nameof(TrainingRow.Sma5Ratio),
        nameof(TrainingRow.Sma20Ratio),
        nameof(TrainingRow.VolumeRatio),
        nameof(TrainingRow.AtrRatio14)
    };

    public static readonly string[] RsiFeatures =
    {
        nameof(TrainingRow.Return1D),
        nameof(TrainingRow.Return5D),
        nameof(TrainingRow.Sma5Ratio),
        nameof(TrainingRow.Sma20Ratio),
        nameof(TrainingRow.VolumeRatio),
        nameof(TrainingRow.AtrRatio14),
        nameof(TrainingRow.Rsi14)
    };

    // Checkpoint 2 — Feature Engineering (ADX).
    // Base + ADX14 only; excludes RSI14 (rejected in FeatureSelection).
    // Tests ADX vs baseline, not atop a rejected feature.
    public static readonly string[] BaseAdxFeatures =
    {
        nameof(TrainingRow.Return1D),
        nameof(TrainingRow.Return5D),
        nameof(TrainingRow.Sma5Ratio),
        nameof(TrainingRow.Sma20Ratio),
        nameof(TrainingRow.VolumeRatio),
        nameof(TrainingRow.AtrRatio14),
        nameof(TrainingRow.Adx14)
    };
}


public enum FeatureSetType
{
    Base,
    Rsi,
    BaseADX
}