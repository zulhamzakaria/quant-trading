using QuantTrading.Domain.Entities;
using QuantTrading.Domain.ValueObjects;

namespace QuantTrading.Domain.Models;

public sealed record BacktestResult
{
    public string StrategyName { get; }

    public DateTime StartDate { get; }

    public DateTime EndDate { get; }

    public Money InitialCapital { get; }

    public Money FinalCapital { get; }

    public int TotalTrades { get; }

    public int WinningTrades { get; }

    /// <summary>
    /// Largest peak-to-trough portfolio decline during the backtest.
    /// Represented as a decimal percentage (e.g. 0.25 = 25%).
    /// </summary>
    public decimal MaxDrawdown { get; }

    /// <summary>
    /// Risk-adjusted return metric.
    /// Higher is generally better.
    /// </summary>
    public decimal SharpeRatio { get; }

    public IReadOnlyList<Trade> Trades { get; }

    public decimal TotalReturn =>
        InitialCapital.Amount > 0
            ? (FinalCapital.Amount - InitialCapital.Amount)
                / InitialCapital.Amount
            : 0;

    public decimal WinRate =>
        TotalTrades > 0
            ? (decimal)WinningTrades / TotalTrades
            : 0;

    public BacktestResult(
        string strategyName,
        DateTime startDate,
        DateTime endDate,
        Money initialCapital,
        Money finalCapital,
        int totalTrades,
        int winningTrades,
        decimal maxDrawdown,
        decimal sharpeRatio,
        IEnumerable<Trade>? trades = null)
    {
        strategyName = strategyName?.Trim();

        if (string.IsNullOrWhiteSpace(strategyName))
            throw new ArgumentException(
                "Strategy name is required.",
                nameof(strategyName));

        if (endDate < startDate)
            throw new ArgumentException(
                "End date cannot be earlier than start date.",
                nameof(endDate));

        if (initialCapital is null)
            throw new ArgumentNullException(nameof(initialCapital));

        if (finalCapital is null)
            throw new ArgumentNullException(nameof(finalCapital));

        if (initialCapital.Currency != finalCapital.Currency)
            throw new InvalidOperationException(
                "Initial and final capital currencies must match.");

        if (totalTrades < 0)
            throw new ArgumentOutOfRangeException(
                nameof(totalTrades),
                "Total trades cannot be negative.");

        if (winningTrades < 0 || winningTrades > totalTrades)
            throw new ArgumentOutOfRangeException(
                nameof(winningTrades),
                "Winning trades must be between 0 and total trades.");

        if (maxDrawdown < 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxDrawdown),
                "Max drawdown cannot be negative.");

        StrategyName = strategyName;
        StartDate = startDate;
        EndDate = endDate;
        InitialCapital = initialCapital;
        FinalCapital = finalCapital;
        TotalTrades = totalTrades;
        WinningTrades = winningTrades;
        MaxDrawdown = maxDrawdown;
        SharpeRatio = sharpeRatio;

        Trades = trades?.ToList().AsReadOnly()
            ?? new List<Trade>().AsReadOnly();
    }
}