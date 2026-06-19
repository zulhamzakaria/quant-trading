using QuantTrading.Shared.Execution;

namespace QuantTrading.Simulation.Execution;

public sealed record FillReceipt(
    string Symbol,
    OrderAction Action,
    decimal Price,
    int Quantity,
    DateTime Timestamp);
