using QuantTrading.Simulation.Shared;

namespace QuantTrading.Simulation.Execution;

public sealed record ExecutionFill(
    Guid Id,
    string Symbol,
    OrderAction Action,
    decimal Price,
    int Quantity,
    DateTime Timestamp);
