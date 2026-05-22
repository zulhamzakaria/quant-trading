using QuantTrading.Domain.Common;

namespace QuantTrading.Domain.Entities;

public sealed class Portfolio
{
    public Guid Id { get; private set; }
    public decimal CashBalance { get; private set; }

    private readonly Dictionary<string, Position> _openPositions = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<Position> OpenPositions => _openPositions.Values.ToList().AsReadOnly();

    private readonly List<Position> _historyLog = new();
    public IReadOnlyCollection<Position> HistoryLog => _historyLog.AsReadOnly();

    public decimal CalculateTotalEquity(IReadOnlyDictionary<string, decimal> currentPrices)
    {
        decimal positionsValue = 0m;
        foreach (var position in _openPositions.Values)
        {
            if (!currentPrices.TryGetValue(position.Symbol, out var currentPrice))
            {
                currentPrice = position.AverageEntryPrice;
            }
            positionsValue += position.GetMarketValue(currentPrice);
        }
        return CashBalance + positionsValue;
    }

    public Portfolio() { }

    public static Result<Portfolio> Create(decimal initialBalance)
    {
        if (initialBalance < 0)
            return Result.Failure<Portfolio>(DomainErrors.PortfolioError.InsufficientFunds);

        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            CashBalance = initialBalance
        };
        return Result.Success(portfolio);
    }

    public Result UpdateBalance(decimal amount)
    {
        if (CashBalance + amount < 0)
            return Result.Failure(DomainErrors.PortfolioError.InsufficientFunds);

        CashBalance += amount;

        return Result.Success();
    }

    public bool HasPositionForSymbol(string symbol)
        => _openPositions.ContainsKey(symbol);

    public void OnPositionOpened(Position position)
    {
        _openPositions[position.Symbol] = position;
        _historyLog.Add(position);
        CashBalance -= position.AverageEntryPrice;
    }

    public void OnPositionClosed(string symbol, decimal exitPrice)
    {
        if (_openPositions.Remove(symbol, out var position))
        {
            CashBalance += position.Quantity * exitPrice;
        }
    }

}