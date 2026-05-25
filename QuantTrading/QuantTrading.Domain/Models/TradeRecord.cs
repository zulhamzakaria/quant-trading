namespace QuantTrading.Domain.Models;

public sealed class TradeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // The Foreign Key linking back to the parent BacktestResult summary
    public Guid BacktestResultId { get; set; }

    public string Symbol { get; set; } = null!;
    public decimal Quantity { get; set; }

    // --- Entry Metrics ---
    public decimal EntryPrice { get; set; }
    public DateTimeOffset EnteredAt { get; set; }

    // --- Exit Metrics ---
    public decimal ExitPrice { get; set; }
    public DateTimeOffset ExitedAt { get; set; }

    // --- Performance Outcome ---
    public decimal RealizedPnL { get; set; }

    // Helper computed property to track strategy holding durations
    public TimeSpan Duration => ExitedAt - EnteredAt;
}
