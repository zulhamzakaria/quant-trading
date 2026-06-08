using QuantTrading.Domain.Models;

namespace QuantTrading.ML.Features;

public sealed class FeatureGenerator
{
    public IReadOnlyList<ModelFeatures> ComputeFeatures(IReadOnlyList<MarketData> bars)
    {
        List<ModelFeatures> featuresList = new();

        if (bars.Count < 20)
            return featuresList;
    }
}
