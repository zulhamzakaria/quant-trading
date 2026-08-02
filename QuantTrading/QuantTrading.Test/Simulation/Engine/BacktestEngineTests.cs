using FluentAssertions;
using QuantTrading.Shared.Execution;
using QuantTrading.Simulation.Engine;
using QuantTrading.Simulation.Models;
using QuantTrading.Test.Common.Builders;
using QuantTrading.Test.Common.Fakes;

namespace QuantTrading.Test.Simulation.Engine;

public class BacktestEngineTests
{
    private const string Symbol = "AAPL";

    // Guards against: BacktestEngine.ExecuteOrder's _entryPrices dictionary
    // storing a single (price, timestamp) tuple per symbol. A second Buy
    // before any Sell silently overwrites the first lot's entry price, so
    // the eventual CompletedTrade's RealizedPnL is computed against the
    // wrong cost basis. This test asserts the invariant (total realized
    // P&L = total exit proceeds - total entry cost) rather than a specific
    // CompletedTrade shape, since the fix's exact bookkeeping design
    // (one blended trade vs. per-lot trades) is not yet decided.
    [Trait("Category", "Regression")]
    [Fact]
    public void Given_TwoBuysBeforeOneFullExitSell_When_PositionIsFullyClosed_Then_TotalRealizedPnLReflectsBothEntryPrices()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: 21, price: 100m);

        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 100m, close: 100m); // 1st buy fills here
        var bar24 = MarketDataBuilder.Bar(Symbol, bar23.Timestamp.AddDays(1), open: 110m, close: 110m); // 2nd buy fills here (overwrite)
        var bar25 = MarketDataBuilder.Bar(Symbol, bar24.Timestamp.AddDays(1), open: 120m, close: 120m); // sell fills here

        var feed = warmup.Concat([bar22, bar23, bar24, bar25]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy,  new SizingInstruction.FixedQuantity(10)),
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy,  new SizingInstruction.FixedQuantity(10)),
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Sell, new SizingInstruction.FixedQuantity(20)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        engine.RunSimulation(feed);
        var completedTrades =
            engine.GetCompletedTrades(strategy);
        decimal totalRealizedPnL =
            engine.GetCompletedTrades(strategy)
            .Sum(t => t.RealizedPnL);

        // Assert
        // cost = (10 * 100) + (10 * 110) = 2100 ; proceeds = 20 * 120 = 2400
        totalRealizedPnL.Should().Be(300m);

        // Confirms the FIFO lot-split design decision itself, not just the total:
        // one Sell spanning two lots must produce two CompletedTrade records,
        // each tracing back to its own entry — not a single blended trade.
        completedTrades.Should().HaveCount(2);
        completedTrades[0].Should().BeEquivalentTo(new
        {
            EntryPrice = 100m,
            Quantity = 10,
            ExitPrice = 120m
        }, options => options.ExcludingMissingMembers());
        completedTrades[1].Should().BeEquivalentTo(new
        {
            EntryPrice = 110m,
            Quantity = 10,
            ExitPrice = 120m
        }, options => options.ExcludingMissingMembers());

    }

    // Guards against a regression where a partial Sell caused the strategy's
    // remaining open shares to lose their tracked entry — a later Sell of the
    // remainder would then have nothing to attribute PnL against, and its
    // trade would be silently missing from history. Two Sells against one
    // original Buy must produce two realized trades, both correctly
    // attributed back to that same original entry.
    [Trait("Category", "Regression")]
    [Fact]
    public void Given_PartialSellFollowedByRemainderSell_When_BothExecute_Then_BothRealizedTradesAttributeToTheOriginalEntry()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: 21, price: 100m);

        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 100m, close: 100m); // buy fills here
        var bar24 = MarketDataBuilder.Bar(Symbol, bar23.Timestamp.AddDays(1), open: 110m, close: 110m); // partial sell fills here
        var bar25 = MarketDataBuilder.Bar(Symbol, bar24.Timestamp.AddDays(1), open: 120m, close: 120m); // remainder sell fills here

        var feed = warmup.Concat([bar22, bar23, bar24, bar25]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy,  new SizingInstruction.FixedQuantity(10)),
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Sell, new SizingInstruction.FixedQuantity(4)),
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Sell, new SizingInstruction.FixedQuantity(6)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        engine.RunSimulation(feed);
        var completedTrades = engine.GetCompletedTrades(strategy);

        // Assert
        // Evidence that both sells actually realized PnL as separate trades —
        // the old bug would leave the second sell's trade missing entirely.
        completedTrades.Should().HaveCount(2);

        // The real proof: both trades trace back to the SAME original entry
        // (price and timestamp), showing the remainder stayed correctly
        // attributed after the partial sell — not lost, not treated as an
        // unrelated/unknown entry.
        completedTrades.Select(t => t.EntryPrice).Should().AllBeEquivalentTo(100m);
        completedTrades.Select(t => t.EntryTimestamp).Should().AllBeEquivalentTo(bar23.Timestamp);

        completedTrades[0].Quantity.Should().Be(4);
        completedTrades[0].ExitPrice.Should().Be(110m);
        completedTrades[1].Quantity.Should().Be(6);
        completedTrades[1].ExitPrice.Should().Be(120m);

        completedTrades.Sum(t => t.RealizedPnL).Should().Be(160m); // (110-100)*4 + (120-100)*6
    }

    // Guards against a subtler variant of the FIFO consumption loop than the
    // original regression: a single Sell spanning multiple lots must correctly
    // apply PARTIAL consumption when it lands mid-way through a lot that isn't
    // the first one touched — not just when every lot happens to be fully
    // drained. A loop that only correctly shrinks the very first lot it
    // touches (and blindly fully-drains every subsequent one) would pass an
    // exact-match scenario but fail here.
    [Trait("Category", "Regression")]
    [Fact]
    public void Given_SellSpansThreeLotsWithThirdOnlyPartiallyConsumed_When_SellExecutes_Then_EachLotIsCorrectlyAttributedAndTheRemainderStaysOpen()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: 21, price: 100m);

        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 100m, close: 100m); // lot A fills here
        var bar24 = MarketDataBuilder.Bar(Symbol, bar23.Timestamp.AddDays(1), open: 110m, close: 110m); // lot B fills here
        var bar25 = MarketDataBuilder.Bar(Symbol, bar24.Timestamp.AddDays(1), open: 120m, close: 120m); // lot C fills here
        var bar26 = MarketDataBuilder.Bar(Symbol, bar25.Timestamp.AddDays(1), open: 130m, close: 130m); // sell fills here

        var feed = warmup.Concat([bar22, bar23, bar24, bar25, bar26]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
        new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy,  new SizingInstruction.FixedQuantity(5)),
        new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy,  new SizingInstruction.FixedQuantity(3)),
        new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy,  new SizingInstruction.FixedQuantity(4)),
        new OrderRequest(Symbol, OrderType.Market, OrderAction.Sell, new SizingInstruction.FixedQuantity(10)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        engine.RunSimulation(feed);
        var completedTrades = engine.GetCompletedTrades(strategy);
        var accountState = engine.GetAccountState(strategy);

        // Assert
        // Evidence: three lots were touched, not silently merged or skipped.
        completedTrades.Should().HaveCount(3);

        // Lot A (fully consumed, 1st in the loop)
        completedTrades[0].EntryPrice.Should().Be(100m);
        completedTrades[0].Quantity.Should().Be(5);

        // Lot B (fully consumed, 2nd in the loop)
        completedTrades[1].EntryPrice.Should().Be(110m);
        completedTrades[1].Quantity.Should().Be(3);

        // Lot C (PARTIALLY consumed, 3rd/last in the loop) — the actual proof
        // this test exists for. Only 2 of its original 4 shares were realized.
        completedTrades[2].EntryPrice.Should().Be(120m);
        completedTrades[2].Quantity.Should().Be(2);

        completedTrades.Select(t => t.ExitPrice).Should().AllBeEquivalentTo(130m);
        completedTrades.Sum(t => t.RealizedPnL).Should().Be(230m); // 150 + 60 + 20

        // The remainder of lot C (2 shares) must still be open and correctly
        // tracked — not dropped, not merged into a new/unrelated entry.
        accountState.GetPositionSize(Symbol).Should().Be(2);
        accountState.HasPositionOpen(Symbol).Should().BeTrue();
        engine.GetOpenPositionEntryTimestamp(strategy, Symbol).Should().Be(bar25.Timestamp);
    }
}
