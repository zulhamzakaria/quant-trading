using Microsoft.ML.Data;
using QuantTrading.Shared.Features;

namespace QuantTrading.Shared.Models;

public sealed class TrainingRow
{
    //DateTime Timestamp,
    [ColumnName("Return1D")]
    public float Return1D { get; set; }
    [ColumnName("Return5D")]
    public float Return5D { get; set; }
    [ColumnName("Sma5Ratio")]
    public float Sma5Ratio { get; set; }
    [ColumnName("Sma20Ratio")]
    public float Sma20Ratio { get; set; }
    [ColumnName("VolumeRatio")]
    public float VolumeRatio { get; set; }
    [ColumnName("Rsi14")]
    public float Rsi14 { get; set; }
    [ColumnName("AtrRatio14")]
    public float AtrRatio14 { get; set; }
    [ColumnName("IsTomorrowCloseHigher")]
    public bool IsTomorrowCloseHigher { get; set; }
    [ColumnName("BollingerStdDev20")]
    public float BollingerStdDev20 { get; set; }
    [ColumnName("Adx14")]
    public float Adx14 { get; set; }
    [ColumnName("ObvDeviation20")]
    public float ObvDeviation20 { get; set; }
    [ColumnName("PriceZScore20")]
    public float PriceZScore20 { get; set; }
    public static TrainingRow FromMarketFeatures(MarketFeatures features)
    {
        return new TrainingRow
        {
            Return1D = (float)features.Return1D,
            Return5D = (float)features.Return5D,
            Sma5Ratio = (float)features.Sma5Ratio,
            Sma20Ratio = (float)features.Sma20Ratio,
            VolumeRatio = (float)features.VolumeRatio,
            Rsi14 = (float)features.Rsi14,
            AtrRatio14 = (float)features.AtrRatio14,
            BollingerStdDev20 = (float)features.BollingerStdDev20,
            Adx14 = (float)features.Adx14,
            ObvDeviation20 = (float)features.ObvDeviation20,
            PriceZScore20 = (float)features.PriceZScore20,
            IsTomorrowCloseHigher = false
        };
    }

};

