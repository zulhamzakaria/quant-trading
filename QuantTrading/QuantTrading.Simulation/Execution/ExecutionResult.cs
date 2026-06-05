namespace QuantTrading.Simulation.Execution;

public sealed record ExecutionResult(
    bool IsSuccess, string RejectionReason, FillReceipt? FilledOrder);
