using QuantTrading.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuantTrading.Domain.Entities;

public sealed class Portfolio
{
    public Guid Id { get; private set; }
    public decimal CashBalance { get; private set; }

    private readonly List<Position> _positions = new();
    public IReadOnlyCollection<Position> Positions => _positions.AsReadOnly();

    [NotMapped]
    public IReadOnlyCollection<Position> OpenPositions => _positions
        .Where(p => p.IsOpen)
        .ToList()
        .AsReadOnly();

    public Portfolio() { }

    public bool HasOpenPositionForSymbol(string symbol)
    {
        return _positions.Any(p => p.IsOpen && p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
    }

    public void OpenNewPosition(Position position)
    {
        _positions.Add(position);
        CashBalance -= position.CostBasis;
    }
    public decimal CalculateTotalEquity(IReadOnlyDictionary<string, decimal> currentPrices)
    {
        decimal positionsValue = 0m;
        foreach (var position in _positions.Where(p => p.IsOpen))
        {
            if (!currentPrices.TryGetValue(position.Symbol, out var currentPrice))
            {
                currentPrice = position.AverageEntryPrice;
            }
            positionsValue += position.GetMarketValue(currentPrice);
        }
        return CashBalance + positionsValue;
    }

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

    public void OnPositionClosed(string symbol, decimal exitPrice)
    {
        // Find the active open position for this ticker
        var position = _positions
            .FirstOrDefault(p => p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) && p.IsOpen);

        if (position != null)
        {
            CashBalance += position.Quantity * exitPrice;
            position.ClosePosition();
        }
    }

}