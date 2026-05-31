using QuantTrading.Domain.Models;

namespace QuantTrading.Simulation.Execution;

public sealed record ExecutionResult(
    bool IsSuccess, string RejectionReason, ExecutionFill? FilledTrade);
