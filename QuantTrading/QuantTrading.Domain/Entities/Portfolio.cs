using QuantTrading.Domain.Common;

namespace QuantTrading.Domain.Entities;

public sealed class Portfolio
{
    public Guid Id { get; private set; }
    public decimal CashBalance { get; private set; }

    private readonly List<Position> _positions = new();
    public IReadOnlyCollection<Position> Positions => _positions.AsReadOnly();

    public Portfolio() { }

    public static Result<Portfolio> Create(decimal initialBalance)
    {
        if (initialBalance < 0)
            return Result.Failure<Portfolio>(DomainErrors.Portfolio.InsufficientFunds);

        return new Portfolio
        {
            Id = Guid.NewGuid(),
            CashBalance = initialBalance
        };
    }

    public Result UpdateBalance(decimal amount)
    {
        if (CashBalance + amount < 0)
            return Result.Failure(DomainErrors.Portfolio.InsufficientFunds);

        CashBalance += amount;

        return Result.Success();
    }

}
