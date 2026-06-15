namespace QuantTrading.Domain.Models;

public sealed record MarketData(
    string Symbol,
    DateTime Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);
