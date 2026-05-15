using QuantTrading.Domain.Common;

namespace QuantTrading.Domain.Entities;

public sealed class Trade
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; } = null!;
    public decimal Price { get; private set; }
    public decimal Quantity { get; private set; }
    public TradeSide Side { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }

    private Trade()
    {
        //EF Core requires a parameterless constructor
    }

    public static Result<Trade> Create(string symbol, decimal price, decimal quantity, TradeSide side)
    {
        if(string.IsNullOrWhiteSpace(symbol))
            return Result.Failure<Trade>(DomainErrors.TradeError.InvalidSymbol);
        if(price <= 0)
            return Result.Failure<Trade>(DomainErrors.TradeError.InvalidPrice);
        if(quantity <= 0)
            return Result.Failure<Trade>(DomainErrors.TradeError.InvalidQuantity);
        if(!Enum.IsDefined(typeof(TradeSide), side) || side == TradeSide.None)
            return Result.Failure<Trade>(DomainErrors.TradeError.InvalidSide);

        return new Trade
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            Price = price,
            Quantity = quantity,
            Side = side,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}

