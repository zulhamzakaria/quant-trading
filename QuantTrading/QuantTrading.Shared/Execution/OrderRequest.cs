namespace QuantTrading.Shared.Execution;

public sealed record OrderRequest(
    string Symbol,
    OrderType Type,
    OrderAction Action,
    int Quantity,
    decimal? LimitPrice = null);
