namespace QuantTrading.Simulation.Analytics;

public sealed record BacktestMetrics(
    int TotalTrades,
    decimal WinRate);