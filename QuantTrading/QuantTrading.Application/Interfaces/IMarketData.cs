namespace QuantTrading.Application.Interfaces;

public interface IMarketData
{
    string Symbol { get; }
    DateTime Timestamp { get; }
}
