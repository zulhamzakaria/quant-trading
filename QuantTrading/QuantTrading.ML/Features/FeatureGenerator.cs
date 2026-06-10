using QuantTrading.Domain.Models;

namespace QuantTrading.ML.Features;

public sealed class FeatureGenerator
{
    public IReadOnlyList<TrainingRow> ComputeFeatures(IReadOnlyList<MarketData> bars)
    {
        List<TrainingRow> featureList = new();

        if (bars.Count < 21)
            return featureList;

        for (int i = 19; i < bars.Count - 1; i++)
        {
            var current = bars[i];
            var yesterday = bars[i - 1];
            var fiveDaysAgo = bars[i - 5];

            // Calculate metrics in decimal to preserve precision
            decimal return1D = yesterday.Close != 0 ? (current.Close - yesterday.Close) / yesterday.Close : 0m;
            decimal return5D = fiveDaysAgo.Close != 0 ? (current.Close - fiveDaysAgo.Close) / fiveDaysAgo.Close : 0m;
            var tomorrow = bars[i + 1];

            decimal sumClose5 = 0;
            decimal sumVolume5 = 0;
            for (int j = 0; j < 5; j++)
            {
                sumClose5 += bars[i - j].Close;
                sumVolume5 += bars[i - j].Volume;
            }
            decimal sma5 = sumClose5 / 5;
            decimal avgVol5 = sumVolume5 / 5;

            decimal sumClose20 = 0;
            for (int j = 0; j < 20; j++)
            {
                sumClose20 += bars[i - j].Close;
            }
            decimal sma20 = sumClose20 / 20;

            decimal sma5Ratio = sma5 != 0 ? current.Close / sma5 : 1.0m;
            decimal sma20Ratio = sma20 != 0 ? current.Close / sma20 : 1.0m;
            decimal volumeRatio = avgVol5 != 0 ? current.Volume / avgVol5 : 1.0m;

            bool isTomorrowCloseHigher = tomorrow.Close > current.Close;

            // Cast to float only at the boundary record for ML.NET compatibility (decimal for calculation)
            featureList.Add(new TrainingRow(
                //Timestamp: current.Timestamp,
                Return1D: (float)return1D,
                Return5D: (float)return5D,
                Sma5Ratio: (float)sma5Ratio,
                Sma20Ratio: (float)sma20Ratio,
                VolumeRatio: (float)volumeRatio,
                IsTomorrowCloseHigher: isTomorrowCloseHigher
            ));
        }

        return featureList;

    }
}
