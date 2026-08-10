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

    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_BarCountAtMinBarRequiredBoundary_When_ComputeMarketFeaturesIsCalled_Then_NullBelowAndNonNullAtThreshold()
    {
        var generator = new FeatureGenerator();
        var bars21 = MarketDataBuilder.FlatBars("AAPL", count: 21, price: 100m);
        var bars22 = MarketDataBuilder.FlatBars("AAPL", count: 22, price: 100m);

        generator.ComputeMarketFeatures(bars21).Should().BeNull();
        generator.ComputeMarketFeatures(bars22).Should().NotBeNull();
    }
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_AKnownPriceSeries_When_FeaturesAreComputed_Then_SmaBollingerAndRatiosMatchHandDerivedValues()
    {
        // Arrange
        var generator = new FeatureGenerator();
        var closes = new decimal[22];
        var volumes = new decimal[22];

        // Indices 0-1: unused filler (outside the 20-bar window for index 21).
        closes[0] = closes[1] = 100m;
        volumes[0] = volumes[1] = 900m;

        // Indices 2-21: 10 bars @ 40, 10 bars @ 160 (positions per hand-derivation).
        int[] lowPositions = { 2, 3, 4, 5, 6, 7, 17, 18, 19, 21 };
        for (int i = 2; i <= 21; i++)
        {
            closes[i] = lowPositions.Contains(i) ? 40m : 160m;
            volumes[i] = 900m;
        }
        volumes[21] = 1400m; // current bar's volume, deliberately different for a non-trivial VolumeRatio

        var timestamp = new DateTime(2024, 1, 1);
        var bars = Enumerable.Range(0, 22)
            .Select(i => MarketDataBuilder.Bar("AAPL", timestamp.AddDays(i), open: closes[i], close: closes[i], volume: volumes[i]))
            .ToList();

        // Act
        var features = generator.ComputeMarketFeatures(bars);

        // Assert
        features.Should().NotBeNull();
        features!.Sma5.Should().Be(64m);
        features.Sma20.Should().Be(100m);
        features.Sma5Ratio.Should().Be(0.625m);
        features.Sma20Ratio.Should().Be(0.4m);
        features.BollingerStdDev20.Should().Be(60m);
        features.Return1D.Should().Be(-0.75m);
        features.Return5D.Should().Be(-0.75m);
        features.VolumeRatio.Should().Be(1.4m);
    }
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_AlternatingPriceSeries_When_FeaturesAreComputed_Then_AtrAndRsiMatchIndependentlyComputedValues()
    {
        // Arrange — alternating changes of exactly +10/-10 every day.
        // Parity chosen so the final (current) bar lands on 100, not 110 —
        // 110 has a factor of 11, which breaks AtrRatio14's exact termination.
        var generator = new FeatureGenerator();
        var bars = new List<QuantTrading.Shared.Models.MarketData>();
        var timestamp = new DateTime(2024, 1, 1);
        for (int i = 0; i <= 21; i++)
        {
            decimal close = (i % 2 == 0) ? 110m : 100m;
            bars.Add(MarketDataBuilder.Bar("AAPL", timestamp.AddDays(i), open: close, close: close));
        }

        // Act
        var features = generator.ComputeMarketFeatures(bars);

        // Assert
        features.Should().NotBeNull();

        // ATR: every day's True Range is identically 10 -> Wilder-recursion fixed
        // point, exact by construction. current.Close = 100 -> 10/100 = 0.1 exact.
        features!.AtrRatio14.Should().Be(0.1m);

        // RSI: gain/loss alternates, recursion doesn't terminate in decimal
        // (denominator includes 7^7) — tolerance-based comparison, computed
        // independently via exact Fraction arithmetic.
        features.Rsi14.Should().BeApproximately(47.0458135m, 0.0000001m);
    }
    // ADX's DX-averaging has an undocumented behavioral discontinuity: below 14
    // accumulated DX values it falls back to a plain average; at 14+ it switches
    // to full Wilder-smoothed averaging. Every prior test (4, 5) used the
    // minimum 22-bar setup, which only ever reaches 8 DX values — meaning the
    // Wilder-smoothed branch has never actually been exercised until this test.
    // Scoped as sanity, not exact-value: ADX's DM/DI/DX chain is too deep to
    // hand-derive reliably without TA-Lib (unavailable in this sandbox) — see
    // ledger's Reference Validation gap. Proves both branches are reachable and
    // produce bounded, valid output; does NOT prove the transition is smooth.
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_BarCountBelowAndAboveTheAdxFallbackThreshold_When_FeaturesAreComputed_Then_Adx14IsValidInBothBranches()
    {
        var generator = new FeatureGenerator();

        // Fallback branch: dxList.Count = index-13 < 14 -> index < 27 -> 27 bars.
        var bars27 = MarketDataBuilder.FlatBars("AAPL", count: 27, price: 100m);
        // Main Wilder-smoothed branch: index >= 27 -> 28 bars.
        var bars28 = MarketDataBuilder.FlatBars("AAPL", count: 28, price: 100m);

        var fallback = generator.ComputeMarketFeatures(bars27);
        var main = generator.ComputeMarketFeatures(bars28);

        fallback.Should().NotBeNull();
        main.Should().NotBeNull();

        fallback!.Adx14.Should().BeInRange(0m, 100m);
        main!.Adx14.Should().BeInRange(0m, 100m);
    }
}