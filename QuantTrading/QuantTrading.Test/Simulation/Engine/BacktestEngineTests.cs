using FluentAssertions;
using QuantTrading.Shared.Execution;
using QuantTrading.Simulation.Engine;
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
        decimal totalRealizedPnL = engine.GetCompletedTrades(strategy).Sum(t => t.RealizedPnL);

        // Assert
        // cost = (10 * 100) + (10 * 110) = 2100 ; proceeds = 20 * 120 = 2400
        totalRealizedPnL.Should().Be(300m);
    }
}
