using FluentAssertions;
using QuantTrading.Shared.Execution;
using QuantTrading.Simulation.Engine;
using QuantTrading.Test.Common.Builders;
using QuantTrading.Test.Common.Fakes;

namespace QuantTrading.Test.Simulation.Engine;

public class BacktestEngineTests
{
    private const string Symbol = "AAPL";
    public const int WarmupBarsRequired = 21;

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
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: WarmupBarsRequired, price: 100m);

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
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: WarmupBarsRequired, price: 100m);

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
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: WarmupBarsRequired, price: 100m);

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

    // Financial Invariant, not a Regression test — no known bug here, protecting
    // currently-correct behavior against a future accidental change. Cash must
    // never be debited, and no position/trade may be recorded, for a Buy the
    // account cannot afford — ExecuteOrder's cash-sufficiency guard must remain
    // an all-or-nothing gate, not something that partially executes.
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_BuyOrderExceedsAvailableCash_When_OrderIsExecuted_Then_NoStateIsMutated()
    {
        // Arrange
        const decimal startingCash = 500m; // deliberately less than the Buy's cost
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: WarmupBarsRequired, price: 100m);
        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 100m, close: 100m); // attempted buy fills here

        var feed = warmup.Concat([bar22, bar23]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
            // 10 shares @ ~100 = 1000, but only 500 cash is available.
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy, new SizingInstruction.FixedQuantity(10)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash);

        // Act
        engine.RunSimulation(feed);
        var accountState = engine.GetAccountState(strategy);
        var completedTrades = engine.GetCompletedTrades(strategy);

        // Assert
        accountState.Cash.Should().Be(startingCash); // nothing debited
        accountState.HasPositionOpen(Symbol).Should().BeFalse();
        accountState.GetPositionSize(Symbol).Should().Be(0);
        completedTrades.Should().BeEmpty();
    }

    // Financial Invariant — Sell-side mirror of the insufficient-cash test. A Sell
    // requesting more shares than are actually held must be an all-or-nothing
    // no-op: no cash credited, no position change, no CompletedTrade recorded —
    // not a partial fill of whatever shares happen to be available.
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_SellOrderExceedsHeldShares_When_OrderIsExecuted_Then_NoStateIsMutated()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: WarmupBarsRequired, price: 100m);
        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 100m, close: 100m); // buy fills here
        var bar24 = MarketDataBuilder.Bar(Symbol, bar23.Timestamp.AddDays(1), open: 110m, close: 110m); // attempted oversized sell fills here

        var feed = warmup.Concat([bar22, bar23, bar24]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
            // Buy 5 shares
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy,  new SizingInstruction.FixedQuantity(5)),
            // Then requests 10.
            new OrderRequest(Symbol, OrderType.Market, OrderAction.Sell, new SizingInstruction.FixedQuantity(10)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        engine.RunSimulation(feed);
        var accountState = engine.GetAccountState(strategy);
        var completedTrades = engine.GetCompletedTrades(strategy);

        // Assert
        accountState.Cash.Should().Be(10_000m - (5 * 100m)); // only the buy's cost was ever debited
        accountState.HasPositionOpen(Symbol).Should().BeTrue();
        accountState.GetPositionSize(Symbol).Should().Be(5); // unchanged by the failed sell
        completedTrades.Should().BeEmpty(); // the failed sell recorded nothing
    }

    // Business Rule — a pending order must be cancelled, not executed or carried
    // forward, if the bar it would execute against is invalid (Open <= 0 or
    // Close <= 0). Observable behavior only: no state mutation from the
    // cancelled order, and the engine must recover and keep calling the
    // strategy on the next valid bar.
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_PendingOrder_When_NextBarIsInvalid_Then_OrderIsCancelledNotExecuted()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: WarmupBarsRequired, price: 100m);
        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var invalidBar = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 0m, close: 0m); // Buy would have fired here
        var bar24 = MarketDataBuilder.Bar(Symbol, invalidBar.Timestamp.AddDays(1), open: 105m, close: 105m); // engine should recover here

        var feed = warmup.Concat([bar22, invalidBar, bar24]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
        new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy, new SizingInstruction.FixedQuantity(10)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        engine.RunSimulation(feed);
        var accountState = engine.GetAccountState(strategy);

        // Assert
        accountState.HasPositionOpen(Symbol).Should().BeFalse(); // cancelled, not executed
        accountState.Cash.Should().Be(10_000m); // nothing debited
        strategy.CallCount.Should().Be(2); // engine kept calling OnData on bar22 and bar24;
                                           // invalid bar is skipped entirely, never triggers OnData
    }

    // Business Rule — a pending order still unexecuted at the end of the feed
    // must be discarded, not force-executed or carried into a hypothetical next
    // run. Observable behavior: the position it would have closed remains open.
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_PendingOrder_When_EndOfFeedIsReached_Then_OrderIsDiscardedNotExecuted()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: WarmupBarsRequired, price: 100m);
        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 100m);
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 100m, close: 100m); // buy fills here, sell then goes pending

        var feed = warmup.Concat([bar22, bar23]).ToList(); // feed ends here — no bar for the sell to execute against

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
        new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy,  new SizingInstruction.FixedQuantity(10)),
        new OrderRequest(Symbol, OrderType.Market, OrderAction.Sell, new SizingInstruction.FixedQuantity(10)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        engine.RunSimulation(feed);
        var accountState = engine.GetAccountState(strategy);
        var completedTrades = engine.GetCompletedTrades(strategy);

        // Assert
        accountState.HasPositionOpen(Symbol).Should().BeTrue(); // sell never executed
        accountState.GetPositionSize(Symbol).Should().Be(10);
        completedTrades.Should().BeEmpty(); // no phantom exit recorded
    }

    // Financial Invariant — pins down the FULL T+1-Open execution contract, not
    // just "not same-bar." Signal-bar Close, T+1 Open, and T+1 Close are all
    // deliberately distinct values here, so a match against the wrong one is
    // unambiguous rather than coincidental (the existing tests 1-3 use flat bars
    // where Open==Close, so they exercise T+1 timing but can't actually
    // distinguish which specific rule ran).
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_SignalGeneratedOnBarT_When_NextBarArrives_Then_OrderExecutesAtBarTPlusOneOpenNotCloseOrSameBar()
    {
        // Arrange
        var warmup = MarketDataBuilder.FlatBars(Symbol, count: 21, price: 100m);

        // Signal bar — OnData fires here, strategy returns Buy. Close (105) must
        // NOT be the execution price (would indicate same-bar execution).
        var bar22 = MarketDataBuilder.Bar(Symbol, warmup[^1].Timestamp.AddDays(1), open: 100m, close: 105m);

        // T+1 — Buy should execute at this bar's Open (110), not its Close (115).
        var bar23 = MarketDataBuilder.Bar(Symbol, bar22.Timestamp.AddDays(1), open: 110m, close: 115m);

        // T+2 — Sell should execute at this bar's Open (120), not its Close (125).
        var bar24 = MarketDataBuilder.Bar(Symbol, bar23.Timestamp.AddDays(1), open: 120m, close: 125m);

        var feed = warmup.Concat([bar22, bar23, bar24]).ToList();

        var strategy = new ScriptedStrategy(new OrderRequest?[]
        {
        new OrderRequest(Symbol, OrderType.Market, OrderAction.Buy,  new SizingInstruction.FixedQuantity(10)),
        new OrderRequest(Symbol, OrderType.Market, OrderAction.Sell, new SizingInstruction.FixedQuantity(10)),
        });

        var engine = new BacktestEngine();
        engine.RegisterStrategy(strategy, startingCash: 10_000m);

        // Act
        engine.RunSimulation(feed);
        var completedTrades = engine.GetCompletedTrades(strategy);

        // Assert
        completedTrades.Should().HaveCount(1);

        var trade = completedTrades[0];

        // Rules out same-bar (105) and T+1-Close (115) in one check.
        trade.EntryPrice.Should().Be(110m);
        trade.EntryTimestamp.Should().Be(bar23.Timestamp);

        trade.ExitPrice.Should().Be(120m);
        trade.ExitTimestamp.Should().Be(bar24.Timestamp);
    }
}
