namespace QuantTrading.Domain.Common;

public enum TradeSide
{
    None = 0,
    Buy = 1,
    Sell = 2
}

public enum SignalType
{
    Hold = 0,
    Bullish = 1, // Price goes up
    Bearish = 2  // Price goes down
}

public enum PositionStatus
{
    Open,
    Closed
}
