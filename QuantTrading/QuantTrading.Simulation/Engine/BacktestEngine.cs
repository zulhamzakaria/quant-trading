using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;
using QuantTrading.Simulation.Models;

namespace QuantTrading.Simulation.Engine;

public sealed class BacktestEngine
{
    private readonly List<IStrategy> _strategies = new();
    private readonly Dictionary<IStrategy, StrategyAccountState>
        _strategyAccounts = new();
    private readonly Dictionary<string, decimal>
        _latestPrices = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<IStrategy, OrderRequest?>
        _pendingOrders = new();

    private readonly Dictionary<IStrategy, List<CompletedTrade>>
        _completedTrades = new();

    private readonly
        Dictionary<IStrategy, Dictionary<string, (decimal price, DateTime timeStamp)>>
        _entryPrices = new();


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
        _entryPrices[strategy] =
            new Dictionary<string, (decimal, DateTime)>
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
                        _entryPrices[strategy]);
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
        Dictionary<string, (decimal price, DateTime timestamp)> entryPrices)
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

                entryPrices[request.Symbol] =
                    (executionPrice, executionTimestamp);
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

                    if (entryPrices.TryGetValue
                        (request.Symbol, out var entry))
                    {
                        completedTrades.Add(new CompletedTrade(
                            Symbol: request.Symbol,
                            EntryPrice: entry.price,
                            ExitPrice: executionPrice,
                            Quantity: quantity,
                            EntryTimestamp: entry.timestamp,
                            ExitTimestamp: executionTimestamp));
                        entryPrices.Remove(request.Symbol);
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
    // a symbol, or null if no position is open. Same reporting-accessor
    // pattern as GetEquityCurve/GetCompletedTrades/GetAccountState — read-only
    // simulation state exposed for post-run analysis, not used by simulation
    // logic itself.
    public DateTime? GetOpenPositionEntryTimestamp(IStrategy strategy, string symbol)
    {
        if (strategy is null)
            throw new ArgumentNullException(nameof(strategy));
        if (!_entryPrices.TryGetValue(strategy, out var symbolEntries))
            throw new KeyNotFoundException(
                "No active account state registry found for the provided strategy instance.");

        return symbolEntries.TryGetValue(symbol, out var entry)
            ? entry.timeStamp
            : null;
    }
}
