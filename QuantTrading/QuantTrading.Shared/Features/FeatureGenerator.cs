using QuantTrading.Shared.Models;

namespace QuantTrading.Shared.Features;

public sealed class FeatureGenerator
{

    private const int MinBarRequired = 22;
    private const int AdxPeriod = 14;

    // shared among some of the indicators
    private const int StandardWindow = 20;

    public IReadOnlyList<TrainingRow> ComputeTrainingRows
        (IReadOnlyList<MarketData> bars)
    {
        List<TrainingRow> featureList = new();
        if (bars is null || bars.Count < MinBarRequired)
            return featureList;

        for (int i = 20; i < bars.Count - 1; i++)
        {
            var features = ComputeMarketFeaturesAt(bars, i);
            if (features is null)
                continue;

            bool isTomorrowCloseHigher =
                 bars[i + 1].Close > bars[i].Close;

            featureList.Add
                (ToTrainingRow(features, isTomorrowCloseHigher));
        }

        return featureList;

    }

    public TrainingRow? ComputeTrainingRow
        (IReadOnlyList<MarketData> bars)
    {
        if (bars is null || bars.Count < MinBarRequired)
            return null;

        var features = ComputeMarketFeaturesAt
            (bars, bars.Count - 1);
        if (features is null)
            return null;

        return ToTrainingRow
            (features, isTomorrowCloseHigher: false);
    }

    public MarketFeatures? ComputeMarketFeatures
        (IReadOnlyList<MarketData> bars)
    {
        if (bars is null || bars.Count < MinBarRequired)
            return null;

        return ComputeMarketFeaturesAt(bars, bars.Count - 1);
    }

    private MarketFeatures? ComputeMarketFeaturesAt
        (IReadOnlyList<MarketData> bars, int index)
    {

        if (index < 20 || index >= bars.Count)
            return null;

        var current = bars[index];
        var yesterday = bars[index - 1];
        var fiveDaysAgo = bars[index - 5];

        decimal return1D =
            yesterday.Close != 0
            ? (current.Close - yesterday.Close) / yesterday.Close
            : 0m;
        decimal return5D =
            fiveDaysAgo.Close != 0
            ? (current.Close - fiveDaysAgo.Close) / fiveDaysAgo.Close
            : 0m;

        // 5-day SMA
        decimal sumClose5 = 0m;
        decimal sumVolume5 = 0m;
        for (int j = 0; j < 5; j++)
        {
            var b = bars[index - j];
            sumClose5 += b.Close;
            sumVolume5 += b.Volume;
        }
        decimal sma5 = sumClose5 / 5m;
        decimal avgVol5 = sumVolume5 / 5m;

        // 20-day SMA
        decimal sumClose20 = 0m;
        for (int j = 0; j < 20; j++)
        {
            sumClose20 += bars[index - j].Close;
        }
        decimal sma20 = sumClose20 / 20m;

        // 20-day Standard Deviation (for Bollinger Bands)
        decimal sumSquaredDiff20 = 0m;
        for (int j = 0; j < 20; j++)
        {
            decimal diff = bars[index - j].Close - sma20;
            sumSquaredDiff20 += diff * diff;
        }
        decimal bollingerStdDev20 =
            (decimal)Math.Sqrt((double)(sumSquaredDiff20 / 20m));

        decimal sma5Ratio =
            sma5 != 0 ? current.Close / sma5 : 1.0m;
        decimal sma20Ratio =
            sma20 != 0 ? current.Close / sma20 : 1.0m;
        decimal volumeRatio =
            avgVol5 != 0 ? current.Volume / avgVol5 : 1.0m;

        decimal avgGain = 0m;
        decimal avgLoss = 0m;

        int startRsiIndex = index - 14;
        if (startRsiIndex < 1)
            startRsiIndex = 1;

        for (int k = startRsiIndex;
            k < startRsiIndex + 14 && k <= index;
            k++)
        {
            decimal change = bars[k].Close - bars[k - 1].Close;
            if (change > 0)
                avgGain += change;
            else
                avgLoss += Math.Abs(change);
        }

        avgGain /= 14m;
        avgLoss /= 14m;

        for (int m = startRsiIndex + 14; m <= index; m++)
        {
            decimal change =
                bars[m].Close - bars[m - 1].Close;
            decimal gain =
                change > 0 ? change : 0m;
            decimal loss =
                change < 0 ? Math.Abs(change) : 0m;

            avgGain = ((avgGain * 13m) + gain) / 14m;
            avgLoss = ((avgLoss * 13m) + loss) / 14m;
        }

        decimal rsi14 = 50m;
        if (avgLoss > 0)
        {
            decimal rs = avgGain / avgLoss;
            rsi14 = 100m - (100m / (1m + rs));
        }
        else if (avgGain > 0)
        {
            rsi14 = 100m;
        }

        decimal avgAtr = 0m;
        int startAtrIndex = index - 14;
        if (startAtrIndex < 1)
            startAtrIndex = 1;

        for (int k = startAtrIndex;
            k < startAtrIndex + 14 && k <= index;
            k++)
        {
            avgAtr +=
                CalculateTrueRange(bars[k], bars[k - 1]);
        }
        avgAtr /= 14m;

        for (int m = startAtrIndex + 14; m <= index; m++)
        {
            decimal tr =
                CalculateTrueRange(bars[m], bars[m - 1]);
            avgAtr = ((avgAtr * 13m) + tr) / 14m;
        }

        decimal atrRatio14 =
            current.Close != 0
            ? avgAtr / current.Close
            : 0m;

        decimal adx14 = CalculateAdx14(bars, index);
        decimal obvDeviation20 =
            CalculateObvDeviation20(bars, index);
        decimal priceZScore20 =
            bollingerStdDev20 != 0
            ? (current.Close - sma20) / bollingerStdDev20
            : 0m;


        return new MarketFeatures(
            Symbol: current.Symbol,
            Timestamp: current.Timestamp,
            Sma5: sma5,
            Sma20: sma20,
            Sma5Ratio: sma5Ratio,
            Sma20Ratio: sma20Ratio,
            Rsi14: rsi14,
            AtrRatio14: atrRatio14,
            BollingerStdDev20: bollingerStdDev20,
            Return1D: return1D,
            Return5D: return5D,
            VolumeRatio: volumeRatio,
            Adx14: adx14,
            ObvDeviation20: obvDeviation20,
            PriceZScore20: priceZScore20 );
    }

    // OBV Deviation (20-day): normalized On-Balance Volume.
    // Raw OBV is cumulative and history-dependent, so not exposed directly.
    // We subtract its 20-day SMA to remove long-term drift, then divide by
    // 20-day average volume for cross-symbol comparability. No extra scaling
    // (e.g. ×20 or ÷√20) is applied since current tree-based models (FastTree,
    // FastForest) are insensitive to monotonic rescaling. Revisit scaling only
    // if OBV proves useful in later experiments.
    private decimal CalculateObvDeviation20
        (IReadOnlyList<MarketData> bars, int index)
    {

        if (index < StandardWindow)
            return 0m;

        var obv = new decimal[index + 1];
        for (int i = 1; i <= index; i++)
        {
            if (bars[i].Close > bars[i - 1].Close)
                obv[i] = obv[i - 1] + bars[i].Volume;
            else if (bars[i].Close < bars[i - 1].Close)
                obv[i] = obv[i - 1] - bars[i].Volume;
            else
                obv[i] = obv[i - 1];
        }

        decimal sumObv20 = 0m;
        decimal sumVolume20 = 0m;
        for (int j = 0; j < StandardWindow; j++)
        {
            sumObv20 += obv[index - j];
            sumVolume20 += bars[index - j].Volume;
        }

        decimal smaObv20 = sumObv20 / StandardWindow;
        decimal smaVol20 = sumVolume20 / StandardWindow;

        return smaVol20 !=0
            ? (obv[index] - smaObv20) / smaVol20
            : 0m;
    }

    // ADX(14): trend strength, direction-agnostic. Double Wilder smoothing
    // (+DM/-DM/TR → DI → DX → ADX). Window=200 chosen empirically: Wilder’s
    // long-memory smoothing needs ~200 bars to converge. Shorter (e.g. 27)
    // diverges (MAE ~9, max ~42). At 200, error ~0; earlier values are warm-up.
    private const int AdxWindowSize = 200;
    private decimal CalculateAdx14
        (IReadOnlyList<MarketData> bars, int index)
    {
        int start = index - (AdxWindowSize - 1);
        if (start < 1)
            start = 1;

        List<decimal> plusDMList = new();
        List<decimal> minusDMList = new();
        List<decimal> trList = new();

        for (int i = start; i <= index; i++)
        {
            decimal upMove = bars[i].High - bars[i - 1].High;
            decimal downMove = bars[i - 1].Low - bars[i].Low;
            decimal plusDM =
                (upMove > downMove && upMove > 0)
                ? upMove
                : 0m;
            decimal minusDM =
                (downMove > upMove && downMove > 0)
                ? downMove
                : 0m;
            plusDMList.Add(plusDM);
            minusDMList.Add(minusDM);
            decimal tr = 
                CalculateTrueRange(bars[i], bars[i - 1]);
            trList.Add(tr);
        }

        int plusDMListCount = plusDMList.Count;
        if (plusDMListCount < AdxPeriod)
            return 0m;

        decimal avgPlusDM = 0m, avgMinusDM = 0m, avgTR = 0m;
        for (int k = 0; k < AdxPeriod; k++)
        {
            avgPlusDM += plusDMList[k];
            avgMinusDM += minusDMList[k];
            avgTR += trList[k];
        }
        avgPlusDM /= AdxPeriod;
        avgMinusDM /= AdxPeriod;
        avgTR /= AdxPeriod;

        List<decimal> dxList = new();
        dxList.Add(CalculateDx(avgPlusDM, avgMinusDM, avgTR));

        for (int m = AdxPeriod; m < plusDMListCount; m++)
        {
            avgPlusDM =
                ((avgPlusDM * (AdxPeriod - 1)) + plusDMList[m]) / AdxPeriod;
            avgMinusDM =
                ((avgMinusDM * (AdxPeriod - 1)) + minusDMList[m]) / AdxPeriod;
            avgTR =
                ((avgTR * (AdxPeriod - 1)) + trList[m]) / AdxPeriod;
        }

        if (dxList.Count >= AdxPeriod)
        {
            decimal avgDx = 0m;
            for (int k = 0; k < AdxPeriod; k++)
                avgDx += dxList[k];
            avgDx /= AdxPeriod;

            for (int k = AdxPeriod; k < dxList.Count; k++)
                avgDx = ((avgDx * (AdxPeriod - 1)) +
                    dxList[k]) / AdxPeriod;

            return avgDx;
        }
        // Fewer than 14 DX values available — degrade gracefully to a
        // simple average instead of a full Wilder smooth.
        return dxList.Average();
    }

    private decimal CalculateDx(
        decimal avgPlusDM,
        decimal avgMinusDM,
        decimal avgTR)
    {
        decimal plusDi =
            avgTR != 0 ? 100m * avgPlusDM / avgTR : 0m;
        decimal minusDi =
            avgTR != 0 ? 100m * avgMinusDM / avgTR : 0m;

        decimal diSum = plusDi * minusDi;
        return diSum != 0
            ? 100m * Math.Abs(plusDi - minusDi) / diSum
            : 0m;
    }

    private static TrainingRow ToTrainingRow
        (MarketFeatures features, bool isTomorrowCloseHigher)
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
            IsTomorrowCloseHigher = isTomorrowCloseHigher
        };
    }

    private static decimal CalculateTrueRange
        (MarketData current, MarketData prev)
    {
        decimal hL =
            current.High - current.Low;
        decimal hC =
            Math.Abs(current.High - prev.Close);
        decimal lC =
            Math.Abs(current.Low - prev.Close);
        return Math.Max(hL, Math.Max(hC, lC));
    }

}
