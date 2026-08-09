using FluentAssertions;
using QuantTrading.Shared.Features;
using QuantTrading.Test.Common.Builders;

namespace QuantTrading.Test.Shared.Features;

public class FeatureGeneratorTests
{
    // If index 21's features change depending on whether bars exist past
    // index 21, that's lookahead. Truncated array has no future bars at
    // all; full array has 3 deliberately extreme ones right after index 21.
    // Must be identical either way.
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_FutureBarsExistPastTheTargetIndex_When_FeaturesAreComputed_Then_ResultIsIdenticalToNotHavingThem()
    {
        // Arrange
        var generator = new FeatureGenerator();
        var flatBars = MarketDataBuilder.FlatBars("AAPL", count: 22, price: 100m); // indices 0-21

        var futureBars = new List<QuantTrading.Shared.Models.MarketData>
        {
            MarketDataBuilder.Bar("AAPL", flatBars[^1].Timestamp.AddDays(1), open: 999_999m, close: 999_999m, volume: 999_999_999m),
            MarketDataBuilder.Bar("AAPL", flatBars[^1].Timestamp.AddDays(2), open: 999_999m, close: 999_999m, volume: 999_999_999m),
            MarketDataBuilder.Bar("AAPL", flatBars[^1].Timestamp.AddDays(3), open: 999_999m, close: 999_999m, volume: 999_999_999m),
        };

        var fullBars = flatBars.Concat(futureBars).ToList(); // 25 bars, index 21 has future bars after it

        // Act
        var featuresFromTruncated = generator.ComputeMarketFeatures(flatBars); // computed at index 21, no future bars exist
        var seriesFromFull = generator.ComputeMarketFeaturesSeries(fullBars);
        var featuresAtSameIndexFromFull = seriesFromFull[1]; // series[0]=index20, series[1]=index21

        // Assert
        featuresFromTruncated.Should().NotBeNull();
        featuresFromTruncated.Should().BeEquivalentTo(featuresAtSameIndexFromFull);
    }
    // Training label deliberately uses bars[i+1] (the "future" bar). Features
    // for that same row must not — this proves the isolation directly, by
    // changing only the future bar between two otherwise-identical datasets.
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_TwoDatasetsDifferingOnlyInTheFutureBar_When_TrainingRowsAreComputed_Then_OnlyTheLabelDiffersNotTheFeatures()
    {
        // Arrange
        var generator = new FeatureGenerator();
        var sharedBars = MarketDataBuilder.FlatBars("AAPL", count: 21, price: 100m); // indices 0-20

        var variantA = sharedBars.Concat(new[] {
        MarketDataBuilder.Bar(
            "AAPL", 
            sharedBars[^1].Timestamp.AddDays(1), 
            open: 110m, 
            close: 110m)})
            .ToList();

        var variantB = sharedBars.Concat(new[] {
        MarketDataBuilder.Bar(
            "AAPL",
            sharedBars[^1].Timestamp.AddDays(1), 
            open: 90m, 
            close: 90m)})
            .ToList();

        // Act
        var rowsA = generator.ComputeTrainingRows(variantA);
        var rowsB = generator.ComputeTrainingRows(variantB);

        // Assert
        rowsA.Should().HaveCount(1);
        rowsB.Should().HaveCount(1);

        rowsA[0].IsTomorrowCloseHigher.Should().BeTrue();
        rowsB[0].IsTomorrowCloseHigher.Should().BeFalse();

        rowsA[0].Should().BeEquivalentTo(rowsB[0], options =>
            options.Excluding(r => r.IsTomorrowCloseHigher)); // only the label may differ
    }
}