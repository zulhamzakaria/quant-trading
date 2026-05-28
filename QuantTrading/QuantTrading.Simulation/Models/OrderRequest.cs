using QuantTrading.Simulation.Shared;

namespace QuantTrading.Simulation.Models;

public sealed record OrderRequest(
    string Symbol,
    OrderType Type,
    OrderAction Action,
    int Quantity,
    decimal? LimitPrice = null);
