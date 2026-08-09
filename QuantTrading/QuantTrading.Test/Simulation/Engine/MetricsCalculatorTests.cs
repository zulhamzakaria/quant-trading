using FluentAssertions;
using QuantTrading.Simulation.Analytics;
using QuantTrading.Simulation.Models;

namespace QuantTrading.Test.Simulation.Engine;

public class MetricsCalculatorTests
{
    // Zero trades must not null out equity-based metrics — Buy & Hold can
    // hold one position the whole backtest with zero CompletedTrades, but
    // still has a real return. Flat curve (no dip) keeps this test separate
    // from Test 6's drawdown peak/trough coverage.
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_ZeroTrades_When_MetricsAreCalculated_Then_EquityBasedMetricsStillComputeButTradeMetricsAreNull()
    {
        // Arrange
        var trades = new List<CompletedTrade>();
        var firstBar = new DateTime(2024, 1, 1);
        var lastBar = firstBar.AddDays(365);

        var equityCurve = new List<EquityPoint>
        {
            new(firstBar, 10_000m),
            new(lastBar, 12_000m),
        };

        // Act
        var metrics = MetricsCalculator.Calculate(
            trades, startingCapital: 10_000m, firstBar, lastBar, equityCurve);

        // Assert
        metrics.TradeCount.Should().Be(0);
        metrics.Winners.Should().Be(0);
        metrics.Losers.Should().Be(0);
        metrics.BreakEven.Should().Be(0);


        metrics.TotalReturn.Should().Be(20m); // ((12_000 - 10_000) + ???) / 10_000
        metrics.Cagr.Should().BeApproximately(20.0150, 0.001); // tolerance, not decimal-place count
        metrics.MaxDrawdownPercent.Should().Be(0m);

        metrics.WinRate.Should().BeNull();
        metrics.AvgGain.Should().BeNull();
        metrics.AvgLoss.Should().BeNull();
        metrics.Expectancy.Should().BeNull();
        metrics.ProfitFactor.Should().BeNull();

        metrics.GrossProfit.Should().Be(0m);
        metrics.GrossLoss.Should().Be(0m);
        metrics.TotalRealizedPnL.Should().Be(0m);
    }

    // Correctness test for the full trade-statistics branch, computed
    // together from one mixed win/loss/breakeven set. 2 winners (+100,+50),
    // 2 losers (-30,-20), 1 breakeven (0).
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_MixedWinLossBreakEvenTrades_When_MetricsAreCalculated_Then_AllTradeStatisticsAreCorrect()
    {
        // Arrange
        var entry = new DateTime(2024, 1, 1);
        var exit = entry.AddDays(1);

        var trades = new List<CompletedTrade>
        {
            new("AAPL", EntryPrice: 100m, ExitPrice: 200m, Quantity: 1, entry, exit), // +100
            new("AAPL", EntryPrice: 100m, ExitPrice: 150m, Quantity: 1, entry, exit), // +50
            new("AAPL", EntryPrice: 100m, ExitPrice: 70m,  Quantity: 1, entry, exit), // -30
            new("AAPL", EntryPrice: 100m, ExitPrice: 80m,  Quantity: 1, entry, exit), // -20
            new("AAPL", EntryPrice: 100m, ExitPrice: 100m, Quantity: 1, entry, exit), // 0
        };

        var equityCurve = new List<EquityPoint>
        {
            new(entry, 10_000m),
            new(exit, 10_100m), // not asserted on — Test 1/6 own equity-based metrics
        };

        // Act
        var metrics = MetricsCalculator.Calculate(
            trades, startingCapital: 10_000m, entry, exit, equityCurve);

        // Assert
        metrics.TradeCount.Should().Be(5);
        metrics.Winners.Should().Be(2);
        metrics.Losers.Should().Be(2);
        metrics.BreakEven.Should().Be(1);

        metrics.WinRate.Should().Be(50m);
        metrics.AvgGain.Should().Be(75m);
        metrics.AvgLoss.Should().Be(25m);
        metrics.GrossProfit.Should().Be(150m);
        metrics.GrossLoss.Should().Be(50m);
        metrics.ProfitFactor.Should().Be(3m);
        metrics.Expectancy.Should().Be(20m);
        metrics.TotalRealizedPnL.Should().Be(100m);
    }
    // No winners: GrossLoss > 0, so ProfitFactor computes to 0 (a real
    // answer), not null. AvgGain is null (no winners to average).
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_AllLosingTrades_When_MetricsAreCalculated_Then_ProfitFactorIsZeroNotNull()
    {
        // Arrange
        var entry = new DateTime(2024, 1, 1);
        var exit = entry.AddDays(1);

        var trades = new List<CompletedTrade>
        {
            new("AAPL", EntryPrice: 100m, ExitPrice: 70m, Quantity: 1, entry, exit), // -30
            new("AAPL", EntryPrice: 100m, ExitPrice: 80m, Quantity: 1, entry, exit), // -20
            new("AAPL", EntryPrice: 100m, ExitPrice: 90m, Quantity: 1, entry, exit), // -10
        };

        var equityCurve = new List<EquityPoint>
        {
            new(entry, 10_000m),
            new(exit, 9_940m),
        };

        // Act
        var metrics = MetricsCalculator.Calculate(
            trades, startingCapital: 10_000m, entry, exit, equityCurve);

        // Assert
        metrics.Winners.Should().Be(0);
        metrics.Losers.Should().Be(3);
        metrics.WinRate.Should().Be(0m); // decisive trades exist, all lost
        metrics.AvgGain.Should().BeNull();
        metrics.AvgLoss.Should().Be(20m);
        metrics.GrossProfit.Should().Be(0m);
        metrics.GrossLoss.Should().Be(60m);
        metrics.ProfitFactor.Should().Be(0m); // computed, not null — GrossLoss > 0
        metrics.Expectancy.Should().Be(-20m);
        metrics.TotalRealizedPnL.Should().Be(-60m);
        metrics.TradeCount.Should().Be(3);
        metrics.BreakEven.Should().Be(0);
    }

    // No losers: GrossLoss == 0, division guard blocks the calculation, so
    // ProfitFactor is null — meaningfully different from the 0 case above.
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_AllWinningTrades_When_MetricsAreCalculated_Then_ProfitFactorIsNullNotZero()
    {
        // Arrange
        var entry = new DateTime(2024, 1, 1);
        var exit = entry.AddDays(1);

        var trades = new List<CompletedTrade>
        {
            new("AAPL", EntryPrice: 100m, ExitPrice: 130m, Quantity: 1, entry, exit), // +30
            new("AAPL", EntryPrice: 100m, ExitPrice: 120m, Quantity: 1, entry, exit), // +20
            new("AAPL", EntryPrice: 100m, ExitPrice: 110m, Quantity: 1, entry, exit), // +10
        };

        var equityCurve = new List<EquityPoint>
        {
            new(entry, 10_000m),
            new(exit, 10_060m),
        };

        // Act
        var metrics = MetricsCalculator.Calculate(
            trades, startingCapital: 10_000m, entry, exit, equityCurve);

        // Assert
        metrics.Winners.Should().Be(3);
        metrics.Losers.Should().Be(0);
        metrics.WinRate.Should().Be(100m);
        metrics.AvgGain.Should().Be(20m);
        metrics.AvgLoss.Should().BeNull();
        metrics.GrossProfit.Should().Be(60m);
        metrics.GrossLoss.Should().Be(0m);
        metrics.ProfitFactor.Should().BeNull(); // guarded — cannot divide by zero loss
        metrics.Expectancy.Should().Be(20m);
        metrics.TotalRealizedPnL.Should().Be(60m);
    }

    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_SameInstantTimestamps_When_MetricsAreCalculated_Then_CagrIsZero()
    {
        // Arrange
        var trades = new List<CompletedTrade>();
        var timestamp = new DateTime(2024, 1, 1);
        var equityCurve = new List<EquityPoint> { new(timestamp, 10_000m) };

        // Act
        var metrics = MetricsCalculator.Calculate(
            trades, startingCapital: 10_000m, timestamp, timestamp, equityCurve);

        // Assert
        metrics.Cagr.Should().Be(0.0); // exact — hardcoded literal return, no float math involved
    }

    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_PortfolioValueIsZeroOrNegative_When_MetricsAreCalculated_Then_CagrIsNaN()
    {
        // Arrange
        var trades = new List<CompletedTrade>();
        var firstBar = new DateTime(2024, 1, 1);
        var lastBar = firstBar.AddDays(365);
        var equityCurve = new List<EquityPoint>
    {
        new(firstBar, 10_000m),
        new(lastBar, -500m), // wiped out
    };

        // Act
        var metrics = MetricsCalculator.Calculate(
            trades, startingCapital: 10_000m, firstBar, lastBar, equityCurve);

        // Assert
        metrics.Cagr.Should().BeNaN();
    }
    // MaxDrawdown must behave as a running maximum: it has to survive both a
    // new equity peak AND a smaller subsequent drawdown, not just capture
    // whichever dip happens to be first or most recent.
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_MultipleDrawdownsOfDifferingDepth_When_MetricsAreCalculated_Then_MaxDrawdownIsTheTrueMaximumNotTheFirstOrLast()
    {
        // Arrange
        var trades = new List<CompletedTrade>();
        var day0 = new DateTime(2024, 1, 1);

        var equityCurve = new List<EquityPoint>
    {
        new(day0,             10_000m),
        new(day0.AddDays(1),   9_500m), // 5% drawdown
        new(day0.AddDays(2),  11_000m), // new peak
        new(day0.AddDays(3),   9_900m), // 10% drawdown — the true max
        new(day0.AddDays(4),  12_000m), // new peak
        new(day0.AddDays(5),  11_500m), // smaller drawdown — must not overwrite the max
    };

        // Act
        var metrics = MetricsCalculator.Calculate(
            trades, startingCapital: 10_000m, day0, day0.AddDays(5), equityCurve);

        // Assert
        metrics.MaxDrawdownPercent.Should().Be(10m);
    }
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_NonPositiveStartingCapital_When_MetricsAreCalculated_Then_ThrowsArgumentOutOfRangeException()
    {
        var timestamp = new DateTime(2024, 1, 1);
        var equityCurve = new List<EquityPoint> { new(timestamp, 10_000m) };

        Action act = () => MetricsCalculator.Calculate(
            new List<CompletedTrade>(), startingCapital: 0m, timestamp, timestamp, equityCurve);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_NullTrades_When_MetricsAreCalculated_Then_ThrowsArgumentNullException()
    {
        var timestamp = new DateTime(2024, 1, 1);
        var equityCurve = new List<EquityPoint> { new(timestamp, 10_000m) };

        Action act = () => MetricsCalculator.Calculate(
            null!, startingCapital: 10_000m, timestamp, timestamp, equityCurve);

        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_LastBarBeforeFirstBar_When_MetricsAreCalculated_Then_ThrowsArgumentException()
    {
        var firstBar = new DateTime(2024, 1, 2);
        var lastBar = new DateTime(2024, 1, 1); // before firstBar
        var equityCurve = new List<EquityPoint> { new(firstBar, 10_000m) };

        Action act = () => MetricsCalculator.Calculate(
            new List<CompletedTrade>(), startingCapital: 10_000m, firstBar, lastBar, equityCurve);

        act.Should().Throw<ArgumentException>();
    }

    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_EmptyEquityCurve_When_MetricsAreCalculated_Then_ThrowsArgumentException()
    {
        var timestamp = new DateTime(2024, 1, 1);

        Action act = () => MetricsCalculator.Calculate(
            new List<CompletedTrade>(), startingCapital: 10_000m, timestamp, timestamp, new List<EquityPoint>());

        act.Should().Throw<ArgumentException>();
    }

}
