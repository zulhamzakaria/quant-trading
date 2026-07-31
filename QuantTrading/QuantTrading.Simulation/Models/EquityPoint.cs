namespace QuantTrading.Simulation.Models;

public sealed record EquityPoint(
    DateTime Timestamp,
    decimal Equity);