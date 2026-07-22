namespace QuantTrading.Shared.Execution;

public sealed record OrderRequest(
    string Symbol,
    OrderType Type,
    OrderAction Action,
    SizingInstruction Sizing,
    decimal? LimitPrice = null);
