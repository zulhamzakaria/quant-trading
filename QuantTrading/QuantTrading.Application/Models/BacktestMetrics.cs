namespace QuantTrading.Application.Models;

public sealed record BacktestMetrics(
    int TotalTrades,
    decimal WinRate);
