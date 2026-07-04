using Microsoft.ML.Data;

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
};

