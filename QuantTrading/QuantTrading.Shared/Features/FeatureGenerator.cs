using QuantTrading.Shared.Models;

namespace QuantTrading.Shared.Features;

public sealed class FeatureGenerator
{

    private const int MinBarRequired = 22;

    public IReadOnlyList<TrainingRow> ComputeFeatures
        (IReadOnlyList<MarketData> bars)
    {
        List<TrainingRow> featureList = new();
        if (bars is null || bars.Count < MinBarRequired)
            return featureList;

        int totalBars = bars.Count;
        if (totalBars < 22)
            return featureList;

        for (int i = 20; i < bars.Count; i++)
        {
            var tomorrow = bars[i + 1];
            bool isTomorrowCloseHigher =
                tomorrow.Close > bars[i].Close;

            var row = CalculateRowAt
                (bars, i, isTomorrowCloseHigher);
            if (row is not null)
                featureList.Add(row);
        }

        return featureList;

    }

    public TrainingRow? ComputeLatestFeatures
        (IReadOnlyList<MarketData> bars)
    {
        if(bars is null || bars.Count < MinBarRequired)
            return null;
        return CalculateRowAt
            (bars, bars.Count - 1, isTomorrowCloseHigher: false);
    }

    private TrainingRow? CalculateRowAt(
        IReadOnlyList<MarketData> bars,
        int index,
        bool isTomorrowCloseHigher)
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

        for (int m = startRsiIndex + 14;
            m <= index;
            m++)
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
            avgAtr += CalculateTrueRange
                (bars[k], bars[k - 1]);
        }
        avgAtr /= 14m;

        for (int m = startAtrIndex + 14; m <= index; m++)
        {
            decimal tr =
                CalculateTrueRange
                (bars[m], bars[m - 1]);
            avgAtr =
                ((avgAtr * 13m) + tr) / 14m;
        }

        decimal atrRatio14 =
            current.Close != 0
            ? avgAtr / current.Close
            : 0m;

        return new TrainingRow
        {
            Return1D = (float)return1D,
            Return5D = (float)return5D,
            Sma5Ratio = (float)sma5Ratio,
            Sma20Ratio = (float)sma20Ratio,
            VolumeRatio = (float)volumeRatio,
            Rsi14 = (float)rsi14,
            AtrRatio14 = (float)atrRatio14,
            IsTomorrowCloseHigher = isTomorrowCloseHigher
        };
    }

    private decimal CalculateTrueRange
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
