using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;
using QuantTrading.Simulation.Models;
using System.Diagnostics;

namespace QuantTrading.Simulation.Engine;

public sealed class BacktestEngine
{
    // Represents one open buy lot: a distinct entry (price, timestamp, shares)
    // not yet fully consumed by a Sell. Consumed FIFO — oldest lot first.
    // Kept private/internal to BacktestEngine; not part of any public contract.
    private sealed record Lot(decimal Price, DateTime Timestamp, int Shares);

    private readonly List<IStrategy> _strategies = new();
    private readonly Dictionary<IStrategy, StrategyAccountState>
        _strategyAccounts = new();
    private readonly Dictionary<string, decimal>
        _latestPrices = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<IStrategy, OrderRequest?>
        _pendingOrders = new();

    private readonly Dictionary<IStrategy, List<CompletedTrade>>
        _completedTrades = new();

    // Per symbol, an ordered list of open buy lots (FIFO: index 0 = oldest,
    // consumed first by a Sell). Replaces the earlier single-tuple design,
    // which silently overwrote the entry price on a second Buy before any
    // Sell, and dropped trade records entirely on any partial Sell.
    //
    // List<Lot>, not Queue<Lot>: a partial Sell must mutate the front lot's
    // remaining Shares in place while leaving it at the front. Queue<T> only
    // supports Dequeue (removes) and Enqueue (adds to the back) — it cannot
    // express "peek and update the front element," which this algorithm needs
    // on every partial-lot consumption. List<Lot> supports indexed read/write
    // at [0] directly.
    //
    // List.RemoveAt(0) is O(n) in the number of currently-open lots for one
    // symbol — not total trade history. That count is bounded by how many
    // un-closed buys are outstanding at once (realistically single digits),
    // so this is not a performance concern at this project's scale. Revisit
    // only if a strategy's typical open-lot count grows materially.
    private readonly Dictionary
        <IStrategy, Dictionary<string, List<Lot>>>
        _entryLots = new();

    private readonly FeatureGenerator _featureGenerator = new();
    private readonly Dictionary<string, List<MarketData>>
        _barHistory = new(StringComparer.OrdinalIgnoreCase);

    // for data collection only; 
    private readonly Dictionary<IStrategy, List<EquityPoint>>
        _equityCurves = new();
    public void RegisterStrategy(
        IStrategy strategy,
        decimal startingCash,
        string currency = "USD")
    {
        if (strategy is null)
            throw new ArgumentNullException(nameof(strategy));
        if (startingCash <= 0)
            throw new ArgumentOutOfRangeException
                (nameof(startingCash), "Starting cash balance must be greater than zero.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException
                ("Currency cannot be null, empty, or whitespace.", nameof(currency));
        if (_strategyAccounts.ContainsKey(strategy))
            throw new InvalidOperationException
                ($"The strategy instance '{strategy.Name}' is already registered.");

        _strategies.Add(strategy);
        _strategyAccounts[strategy] =
            new StrategyAccountState(startingCash, currency);
        _pendingOrders[strategy] = null;
        _completedTrades[strategy] = new List<CompletedTrade>();
        _entryLots[strategy] = new Dictionary<string, List<Lot>>
            (StringComparer.OrdinalIgnoreCase);
        _equityCurves[strategy] =
            new List<EquityPoint>();
    }

    public void RunSimulation
        (IEnumerable<MarketData> historicalFeed)
    {
        if (historicalFeed is null)
            throw new ArgumentNullException
                (nameof(historicalFeed));
        if (_strategies.Count == 0)
            throw new InvalidOperationException
                ("Cannot run simulation. No strategies registered");

        foreach (var bar in historicalFeed)
        {
            if (bar.Open <= 0 || bar.Close <= 0)
            {
                for (int i = 0; i < _strategies.Count; i++)
                {
                    var strategy = _strategies[i];
                    if (_pendingOrders[strategy] is not null)
                    {
                        Console.WriteLine(
                           $"[ENGINE WARNING] Invalid bar on {bar.Timestamp:yyyy-MM-dd} " +
                           $"for '{strategy.Name}' — pending {_pendingOrders[strategy]!.Action} " +
                           $"order cancelled. Verify dataset integrity.");
                        _pendingOrders[strategy] = null;
                    }
                }
                continue;
            }

            if (!_barHistory.TryGetValue(bar.Symbol, out var history))
            {
                history = new List<MarketData>();
                _barHistory[bar.Symbol] = history;
            }
            history.Add(bar);

            var features = _featureGenerator
                .ComputeMarketFeatures(history);

            for (int i = 0; i < _strategies.Count; i++)
            {
                var strategy = _strategies[i];
                var account = _strategyAccounts[strategy];

                if (_pendingOrders[strategy] is { } pending)
                {
                    ExecuteOrder(
                        strategy,
                        pending,
                        account,
                        bar.Open,
                        bar.Timestamp,
                        _completedTrades[strategy],
                        _entryLots[strategy]);
                    _pendingOrders[strategy] = null;
                }

                if (features is null)
                    continue;

                OrderRequest? request = null;
                try
                {
                    request = strategy.OnData(
                        bar,
                        features,
                        account);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[STRATEGY CRASH] '{strategy.Name}' failed on {bar.Timestamp}: {ex.Message}");
                    continue;
                }
                _pendingOrders[strategy] = request;
            }

            // ADD here — after execution, before equity-curve reporting:
            _latestPrices[bar.Symbol] = bar.Close;

            for (int i = 0; i < _strategies.Count; i++)
            {
                var strategy = _strategies[i];
                decimal equity =
                    CalculateCurrentPortfolioValue(strategy);
                _equityCurves[strategy].Add(new(bar.Timestamp, equity));
            }
        }

        for (int i = 0; i < _strategies.Count; i++)
        {
            var strategy = _strategies[i];
            if (_pendingOrders[strategy] is not null)
            {
                Console.WriteLine(
                    $"[ENGINE INFO] End of feed — pending {_pendingOrders[strategy]!.Action} " +
                    $"order for '{strategy.Name}' discarded. No next bar available for execution.");
                _pendingOrders[strategy] = null;
            }
        }

    }

    private void ExecuteOrder(
        IStrategy strategy,
        OrderRequest request,
        StrategyAccountState account,
        decimal executionPrice,
        DateTime executionTimestamp,
        List<CompletedTrade> completedTrades,
        Dictionary<string, List<Lot>> entryLots)
    {
        if (executionPrice <= 0)
            return;

        int quantity = request.Sizing switch
        {
            SizingInstruction.FixedQuantity fq => fq.Shares,
            SizingInstruction.EquityFraction ef =>
            (int)Math.Floor(CalculateCurrentPortfolioValue(strategy) * ef.Fraction / executionPrice),
            _ => throw new InvalidOperationException($"Unhandled sizing instruction type: {request.Sizing.GetType().Name}")
        };

        if (quantity <= 0)
            return;

        decimal totalValue = quantity * executionPrice;

        if (request.Action == OrderAction.Buy)
        {
            if (account.Cash >= totalValue)
            {
                account.DebitCash(totalValue);
                account.UpdatePosition(
                    request.Symbol,
                    quantity,
                    isExit: false);

                if (!entryLots.TryGetValue(request.Symbol, out var lots))
                {
                    lots = new List<Lot>();
                    entryLots[request.Symbol] = lots;
                }
                lots.Add(new Lot(executionPrice, executionTimestamp, quantity));
            }
        }
        else if (request.Action == OrderAction.Sell)
        {
            if (account.HasPositionOpen(request.Symbol))
            {
                int heldShares =
                    account.GetPositionSize(request.Symbol);
                if (heldShares >= quantity)
                {
                    account.CreditCash(totalValue);
                    account.UpdatePosition(
                        request.Symbol,
                        quantity,
                        isExit: true);

                    // Consume open lots FIFO (oldest first). A single Sell
                    // can close out more than one lot, or partially consume
                    // one — each lot's realized portion is recorded as its
                    // own CompletedTrade. A CompletedTrade therefore
                    // represents one lot's shares (fully or partially)
                    // realized against one Sell execution — not a full
                    // position close, and not one record per Sell order.
                    if (entryLots.TryGetValue(request.Symbol, out var lots))
                    {
                        int remainingToSell = quantity;

                        while (remainingToSell > 0 && lots.Count > 0)
                        {
                            var lot = lots[0];
                            int consumed = Math.Min(remainingToSell, lot.Shares);

                            completedTrades.Add(new CompletedTrade(
                                Symbol: request.Symbol,
                                EntryPrice: lot.Price,
                                ExitPrice: executionPrice,
                                Quantity: consumed,
                                EntryTimestamp: lot.Timestamp,
                                ExitTimestamp: executionTimestamp));

                            if (consumed == lot.Shares)
                                lots.RemoveAt(0);
                            else
                                lots[0] = lot with { Shares = lot.Shares - consumed };

                            remainingToSell -= consumed;
                        }

                        // Accounting invariant: open lot shares for this symbol
                        // must always equal the account's recorded position
                        // size. _entryLots and account._positions are only ever
                        // mutated together, in this method, on the Buy/Sell
                        // branches above — so this cannot currently diverge.
                        // Asserted as insurance against a future change
                        // breaking that pairing, not a currently-reachable
                        // failure. Debug.Assert (not a thrown exception) to
                        // match this project's existing convention for
                        // mathematically-guaranteed invariants (see
                        // MlStrategy.ResolveAtrScaledFraction/
                        // ResolveConfidenceScaledFraction in the handoff doc).
                        int openLotShares = lots.Sum(l => l.Shares);
                        Debug.Assert(
                            openLotShares == account.GetPositionSize(request.Symbol),
                            "Open lot share total diverged from account position size.");
                    }

                }
            }
        }
    }

    public IReadOnlyList<CompletedTrade> GetCompletedTrades
        (IStrategy strategy)
    {
        if (strategy is null)
            throw new ArgumentNullException(nameof(strategy));
        if (!_completedTrades.TryGetValue(strategy, out var trades))
            throw new KeyNotFoundException(
                "No trade history found for the provided strategy instance.");

        return trades.AsReadOnly();
    }

    public decimal CalculateCurrentPortfolioValue
        (IStrategy strategy)
    {
        if (strategy is null)
            throw new ArgumentNullException(nameof(strategy));
        if (!_strategyAccounts.TryGetValue(strategy, out var account))
            throw new KeyNotFoundException
                ("No active account state registry found for the provided strategy instance.");

        decimal inventoryValue = 0m;

        foreach (var kvp in account.ActivePositions)
        {
            string symbol = kvp.Key;
            int shares = kvp.Value;

            if (_latestPrices.TryGetValue(symbol, out decimal currentPrice))
            {
                inventoryValue += shares * currentPrice;
            }
        }

        return account.Cash + inventoryValue;
    }

    // Equity curve uses CalculateCurrentPortfolioValue, which prices via
    // _latestPrices (updated only for the current bar’s symbol). Works for
    // single-symbol data; multi-symbol requires a full pricing model (Phase 5).
    // This is an existing limitation, not a new issue.
    public IReadOnlyList<EquityPoint> GetEquityCurve
            (IStrategy strategy)
    {
        if (strategy is null)
            throw new ArgumentNullException(nameof(strategy));
        if (!_equityCurves.TryGetValue(strategy, out var curve))
            throw new KeyNotFoundException(
                "No equity curve found for the provided strategy instance.");

        return curve.AsReadOnly();
    }

    public IReadonlyAccountState GetAccountState
        (IStrategy strategy)
    {
        if (strategy is null)
            throw new ArgumentNullException(nameof(strategy));

        if (!_strategyAccounts.TryGetValue(strategy, out var state))
            throw new KeyNotFoundException
                ("No active account state registry found for the provided strategy instance.");

        return state;
    }

    // Returns the entry timestamp for a strategy's currently open position in
    // a symbol, or null if no position is open. Under FIFO lot tracking, "the"
    // entry timestamp for an open position is defined as the OLDEST still-open
    // lot's timestamp (front of the list) — i.e. how long the position has
    // been open continuously, which is what ExposureAudit needs. Same
    // reporting-accessor pattern as GetEquityCurve/GetCompletedTrades/
    // GetAccountState — read-only simulation state exposed for post-run
    // analysis, not used by simulation logic itself.
    public DateTime? GetOpenPositionEntryTimestamp(IStrategy strategy, string symbol)
    {
        if (strategy is null)
            throw new ArgumentNullException(nameof(strategy));
        if (!_entryLots.TryGetValue(strategy, out var symbolLots))
            throw new KeyNotFoundException(
                "No active account state registry found for the provided strategy instance.");

        return symbolLots.TryGetValue(symbol, out var lots) && lots.Count > 0
            ? lots[0].Timestamp
            : null;
    }
}
