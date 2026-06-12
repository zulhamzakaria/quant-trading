using QuantTrading.Domain.Models;

namespace QuantTrading.ML.Features;

public sealed class FeatureGenerator
{
    public IReadOnlyList<TrainingRow> ComputeFeatures(IReadOnlyList<MarketData> bars)
    {
        List<TrainingRow> featureList = new();
        int totalBars = bars.Count;
        if (totalBars < 22)
            return featureList;

        decimal[] changes = new decimal[totalBars];
        for (int i = 1; i < totalBars; i++)
        {
            changes[i] = bars[i].Close - bars[i - 1].Close;
        }

        decimal[] wilderAvgGain = new decimal[totalBars];
        decimal[] wilderAvgLoss = new decimal[totalBars];

        decimal seedGain = 0;
        decimal seedLoss = 0;

        for (int k = 1; k <= 14; k++)
        {
            decimal change = changes[k];
            if (change > 0)
                seedGain += change;
            else
                seedLoss += Math.Abs(change);
        }

        wilderAvgGain[14] = seedGain / 14m;
        wilderAvgLoss[14] = seedLoss / 14m;

        for (int i = 15; i < totalBars; i++)
        {
            decimal currentChange = changes[i];
            decimal currentGain =
                currentChange > 0 ? currentChange : 0m;
            decimal currentLoss =
                currentChange < 0 ? Math.Abs(currentChange) : 0m;

            // wilder formula: (prior * 13 + current) / 14
            wilderAvgGain[i]
                = ((wilderAvgGain[i - 1] * 13m) + currentGain) / 14m;
            wilderAvgLoss[i]
                = ((wilderAvgLoss[i - 1] * 13m) + currentLoss) / 14m;
        }

        for (int i = 20; i < bars.Count - 1; i++)
        {
            var current = bars[i];
            var yesterday = bars[i - 1];
            var fiveDaysAgo = bars[i - 5];
            var tomorrow = bars[i + 1];

            // Calculate metrics in decimal to preserve precision
            decimal return1D =
                yesterday.Close != 0 ? (current.Close - yesterday.Close) / yesterday.Close : 0m;
            decimal return5D =
                fiveDaysAgo.Close != 0 ? (current.Close - fiveDaysAgo.Close) / fiveDaysAgo.Close : 0m;

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

            // wilder RSI calculation
            decimal avgGain = wilderAvgGain[i];
            decimal avgLoss = wilderAvgLoss[i];

            // default to 50 (center mark) for no-price movement
            decimal rsi14 = 50m;
            if (avgLoss > 0)
            {
                decimal rs = avgGain / avgLoss;
                rsi14 = 100m - (100m / (1m + rs));
            }
            else if (avgGain > 0)
            {
                rsi14 = 100m; // maxed out RSI when there are gains but no losses
            }

            //bollinger band implementation
            decimal sumOfSquares20 = 0;
            for(int j = 0; j<20; j++)
            {
                decimal deviation = bars[i - j].Close - sma20;
                sumOfSquares20 += deviation * deviation;
            }
            decimal variance20 = sumOfSquares20 / 20m;
            // standard deviation is the square root of variance
            decimal stdDev20 = 
                (decimal)Math.Sqrt((double)variance20);
            decimal bollingerWidth20 =
                sma20 > 0 ? (4m * stdDev20) / sma20 : 0m;

            bool isTomorrowCloseHigher = tomorrow.Close > current.Close;

            // Cast to float only at the boundary record for ML.NET compatibility (decimal for calculation)
            featureList.Add(new TrainingRow(
                //Timestamp: current.Timestamp,
                Return1D: (float)return1D,
                Return5D: (float)return5D,
                Sma5Ratio: (float)sma5Ratio,
                Sma20Ratio: (float)sma20Ratio,
                VolumeRatio: (float)volumeRatio,
                Rsi14: (float)rsi14,
                BollingerWidth20: (float)bollingerWidth20,
                IsTomorrowCloseHigher: isTomorrowCloseHigher
            ));
        }

        if (featureList.Count > 0)
        {
            Console.WriteLine("\n📊 --- DATASET DIAGNOSTIC HARNESS ---");
            Console.WriteLine($"Total Rows Generated : {featureList.Count}");
            Console.WriteLine($"Market Up Days Count : {featureList.Count(x => x.IsTomorrowCloseHigher)} ({((double)featureList.Count(x => x.IsTomorrowCloseHigher) / featureList.Count):P2})");
            Console.WriteLine($"RSI[14] Column Mean  : {featureList.Average(x => x.Rsi14):F2}");
            Console.WriteLine($"RSI[14] Column Max   : {featureList.Max(x => x.Rsi14):F2}");
            Console.WriteLine($"RSI[14] Column Min   : {featureList.Min(x => x.Rsi14):F2}");
            Console.WriteLine($"BB_Width[20] Mean (%) : {featureList.Average(x => x.BollingerWidth20):P2}");
            Console.WriteLine($"BB_Width[20] Max (%)  : {featureList.Max(x => x.BollingerWidth20):P2}");
            Console.WriteLine($"BB_Width[20] Min (%)  : {featureList.Min(x => x.BollingerWidth20):P2}");
            Console.WriteLine("-------------------------------------\n");
        }

        return featureList;

    }
}
