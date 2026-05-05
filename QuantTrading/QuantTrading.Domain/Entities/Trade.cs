using QuantTrading.Domain.Common;
using System.Runtime.InteropServices;

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

    public static Trade Create(string symbol, decimal price, decimal quantity, TradeSide side)
    {
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

