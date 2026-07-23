using QuantTrading.Simulation.Models;

namespace QuantTrading.Simulation.Reporting;

public static class ExposureReporter
{
    public static decimal CalculateExposureRatio(
        IReadOnlyList<CompletedTrade> trades,
        DateTime periodStart,
        DateTime periodEnd,
        DateTime? stillOpenEntryTimestamp = null)
    {
        if (periodEnd <= periodStart)
            throw new ArgumentException("Period end must be after period start.");

        TimeSpan totalPeriod = periodEnd - periodStart;
        TimeSpan timeInMarket = TimeSpan.Zero;

        foreach (var trade in trades)
            timeInMarket +=
                trade.ExitTimestamp - trade.EntryTimestamp;

        if (stillOpenEntryTimestamp is { } openEntry)
            timeInMarket += periodEnd - openEntry;

        return (decimal)(timeInMarket.TotalDays / totalPeriod.TotalDays);
    }
}
