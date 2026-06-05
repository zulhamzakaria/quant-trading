namespace QuantTrading.Simulation.Analytics;

public sealed record ClosedTrade(
    string Symbol,
    decimal EntryPrice,
    decimal ExitPrice,
    int Quantity,
    decimal RealizedPnL,
    DateTime EntryTime,
    DateTime ExitTime)
    {
        public bool IsWin => RealizedPnL > 0;
    }