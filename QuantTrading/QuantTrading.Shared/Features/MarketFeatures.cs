namespace QuantTrading.Shared.Features;

public sealed record MarketFeatures(
    string Symbol,
    DateTime Timestamp,
    decimal Sma5,
    decimal Sma20,
    decimal Sma5Ratio,
    decimal Sma20Ratio,
    decimal Rsi14,
    decimal AtrRatio14,
    decimal BollingerStdDev20,
    decimal Return1D,
    decimal Return5D,
    decimal VolumeRatio,
    decimal Adx14);
