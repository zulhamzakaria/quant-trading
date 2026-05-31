using QuantTrading.Domain.ValueObjects;
using QuantTrading.Simulation.Contracts;
using QuantTrading.Simulation.Models;
using QuantTrading.Simulation.Shared;

namespace QuantTrading.Simulation.Execution;

public sealed class SimulatedBroker
{
    private readonly string _currencyCode;
    private readonly List<ExecutionFill> _fillHistory = new();
    private readonly Dictionary<string, int> _activePositions = new();
    private readonly Dictionary<string, decimal> _latestPrices = new();

    public decimal CashBalance { get; private set; }

    public SimulatedBroker(Money initialCapital)
    {
        CashBalance = initialCapital.Amount;
        _currencyCode = initialCapital.Currency.ToString();
    }

    public void UpdateMarketPrice(string symbol, decimal price)
    {
        _latestPrices[symbol] = price;
    }

    public ExecutionResult ProcessOrder(OrderRequest order, DateTime timestamp)
    {
        if (!_latestPrices.TryGetValue(order.Symbol, out var currentPrice))
        {
            return new ExecutionResult(
                false,
                "Market price not available for symbol: " + order.Symbol,
                null
                );
        }

        decimal totalCost = currentPrice * order.Quantity;

        if (order.Action == OrderAction.Buy)
        {
            if (totalCost > CashBalance)
            {
                return new ExecutionResult(
                    false,
                    "Insufficient cash balance to execute buy order.",
                    null
                    );
            }
            CashBalance -= totalCost;
            _activePositions[order.Symbol] = _activePositions.GetValueOrDefault(order.Symbol) + order.Quantity;
        }
        else if (order.Action == OrderAction.Sell)
        {
            int sharesOwned = _activePositions.GetValueOrDefault(order.Symbol);
            if (sharesOwned < order.Quantity)
            {
                return new ExecutionResult(
                    false,
                    "Insufficient stocks to execute sell order.",
                    null
                    );
            }
            CashBalance += totalCost;
            _activePositions[order.Symbol] = sharesOwned - order.Quantity;
        }

        var fillReceipt = new ExecutionFill(
            Guid.NewGuid(),
            order.Symbol,
            order.Action,
            currentPrice,
            order.Quantity,
            timestamp);

        _fillHistory.Add(fillReceipt);
        return new ExecutionResult(
            true,
            string.Empty,
            fillReceipt);
    }

    public decimal CalculateTotalPortfolioValue()
    {
        decimal positionValue = 0;
        foreach(var kvp in _activePositions)
        {
            decimal lastPrice = _latestPrices.GetValueOrDefault(kvp.Key);
            positionValue += kvp.Value * lastPrice;
        }
        return CashBalance + positionValue;
    }

    public IReadonlyAccountState GetCurrentState()
    {
        var isolatedPositionsSnapshot = new Dictionary<string, int>(_activePositions);
        return new AccountStateSnapshot(CashBalance, _currencyCode, isolatedPositionsSnapshot);
    }

    public IReadOnlyCollection<ExecutionFill> GetExecutedTrades() => _fillHistory.AsReadOnly();
}
