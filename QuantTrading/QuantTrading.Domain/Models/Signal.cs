using QuantTrading.Domain.Common;

namespace QuantTrading.Domain.Models;

public sealed record Signal
{
    public string Symbol { get; }
    public SignalType Type { get; }
    public decimal Confidence { get; }
    public DateTimeOffset Timestamp { get; }
    public string StrategyName { get; }

    public Signal(
        string symbol,
        SignalType type,
        decimal confidence,
        DateTimeOffset timestamp,
        string strategyName
        )
    {
        symbol = symbol?.Trim().ToUpperInvariant() ?? string.Empty;
        strategyName = strategyName?.Trim() ?? string.Empty;

        if(string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol isrequired.", nameof(symbol));
         if(string.IsNullOrWhiteSpace(strategyName))
            throw new ArgumentException("Strategy name is required.", nameof(strategyName));
        if (confidence < 0 || confidence > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");

        Symbol = symbol; 
        Type = type; 
        Confidence = confidence; 
        Timestamp = timestamp; 
        StrategyName = strategyName;

    }
}
