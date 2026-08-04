using FluentAssertions;
using QuantTrading.Shared.Execution;
using QuantTrading.Simulation.Engine;
using QuantTrading.Test.Common.Builders;
using QuantTrading.Test.Common.Fakes;

namespace QuantTrading.Test.Shared.Execution;

public class SizingInstructionTests
{
    private const string Symbol = "AAPL";

    // A rogue SizingInstruction subtype, defined locally to prove the ledger's
    // finding directly: SizingInstruction is abstract+public with no
    // access-restricted constructor, so nothing in the type system stops an
    // external assembly (or this test project) from adding a third case.
    // The hierarchy is closed by convention/documentation only, not by the
    // compiler — this record compiling at all is the proof of that.
    private sealed record RogueSizing(int Value) : SizingInstruction;

    // Financial Invariant — proves the actual safety net: ExecuteOrder's
    // switch throws on any unhandled SizingInstruction case rather than
    // silently mis-sizing an order. This is what makes the "closed hierarchy"
    // assumption safe in practice despite not being compiler-enforced.
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_UnhandledSizingInstructionType_When_OrderIsExecuted_Then_ThrowsInvalidOperationException()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: MarketDataBuilder.WarmupBarsRequired, price: 100m);
        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 100m, close: 100m); // order would execute here

        var feed = warmup.Concat([bar22, bar23]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy, new RogueSizing(10)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        Action act = () => engine.RunSimulation(feed);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    // Financial Invariant — negative FixedQuantity.Shares is not validated at
    // construction; the record accepts any int. Downstream, ExecuteOrder's
    // generic quantity <= 0 guard produces a silent no-op. Asserting the
    // no-mutation outcome directly, same shape as the insufficient-cash/
    // insufficient-shares tests, via this different route into that guard.
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_NegativeFixedQuantityShares_When_OrderIsExecuted_Then_NoStateIsMutated()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: MarketDataBuilder.WarmupBarsRequired, price: 100m);
        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 100m, close: 100m);

        var feed = warmup.Concat([bar22, bar23]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy, new SizingInstruction.FixedQuantity(-5)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        engine.RunSimulation(feed);
        var accountState = engine.GetAccountState(strategy);

        // Assert
        accountState.Cash.Should().Be(10_000m);
        accountState.HasPositionOpen(Symbol).Should().BeFalse();
    }

    // Financial Invariant — EquityFraction.Fraction is not validated at
    // construction; the record accepts any decimal. A negative fraction is
    // caught by the same generic quantity <= 0 guard as FixedQuantity above.
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_NegativeEquityFraction_When_OrderIsExecuted_Then_NoStateIsMutated()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: MarketDataBuilder.WarmupBarsRequired, price: 100m);
        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 100m, close: 100m);

        var feed = warmup.Concat([bar22, bar23]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy, new SizingInstruction.EquityFraction(-0.5m)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        engine.RunSimulation(feed);
        var accountState = engine.GetAccountState(strategy);

        // Assert
        accountState.Cash.Should().Be(10_000m);
        accountState.HasPositionOpen(Symbol).Should().BeFalse();
    }

    // Financial Invariant — a Fraction > 1 ("500% of equity") is caught by a
    // DIFFERENT guard than the negative case above: it resolves to a
    // positive quantity whose cost exceeds available cash, so the Buy
    // branch's cash-sufficiency check (not the quantity <= 0 guard) is what
    // produces the no-op here. Kept as a separate test from the negative
    // case specifically because it exercises a different code path to the
    // same safe outcome — collapsing them into one test would hide that.
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_EquityFractionExceedsOne_When_OrderIsExecuted_Then_NoStateIsMutated()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: MarketDataBuilder.WarmupBarsRequired, price: 100m);
        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 100m, close: 100m);

        var feed = warmup.Concat([bar22, bar23]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
            // 500% of a 10,000 starting portfolio would require 50,000 cash.
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy, new SizingInstruction.EquityFraction(5.0m)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        engine.RunSimulation(feed);
        var accountState = engine.GetAccountState(strategy);

        // Assert
        accountState.Cash.Should().Be(10_000m);
        accountState.HasPositionOpen(Symbol).Should().BeFalse();
    }

    // Business Rule — the EquityFraction happy path has no prior coverage:
    // every other test in this suite uses FixedQuantity exclusively. This
    // proves both halves of one rule together (per the agreed correction to
    // the external review): the exact floor-rounded share count, AND that
    // the result is a whole share count, never rounded up and never left
    // fractional.
    //
    // Cash = 10,000, Fraction = 0.20 (20%), Price = 33.
    // Portfolio value at execution time = starting cash only (no position
    // open yet, so CalculateCurrentPortfolioValue == Cash == 10,000).
    // 10,000 * 0.20 = 2,000 target allocation.
    // 2,000 / 33 = 60.606... shares -> floor -> 60. Never 61, never 60.6.
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_ValidEquityFraction_When_OrderIsExecuted_Then_ResolvesToTheExactFlooredShareCount()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: MarketDataBuilder.WarmupBarsRequired, price: 100m);
        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 33m, close: 33m); // order fills here

        var feed = warmup.Concat([bar22, bar23]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy, new SizingInstruction.EquityFraction(0.20m)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        engine.RunSimulation(feed);
        var accountState = engine.GetAccountState(strategy);

        // Assert
        accountState.GetPositionSize(Symbol).Should().Be(60); // exact floor, never 61
        accountState.Cash.Should().Be(10_000m - (60 * 33m)); // cash reflects the actual filled quantity, not the raw 2,000 target
    }
}