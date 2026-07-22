using QuantTrading.Simulation.Models;

namespace QuantTrading.Simulation.Reporting;

public static class DrawdownAuditReporter
{
    public static DrawdownAuditResult Analyze
        (IReadOnlyList<EquityPoint> equityCurve)
    {
        if (equityCurve is null || equityCurve.Count == 0)
            throw new ArgumentException("Equity curve cannot be null or empty.", nameof(equityCurve));

        decimal peakValue = equityCurve[0].Equity;
        DateTime peakDate = equityCurve[0].Timestamp;

        decimal maxDrawdownPct = 0m;
        DateTime worstPeakDate = peakDate;
        DateTime worstTroughDate = peakDate;
        decimal worstPeakValue = peakValue;
        decimal worstTroughValue = peakValue;

        foreach (var point in equityCurve)
        {
            if (point.Equity > peakValue)
            {
                peakValue = point.Equity;
                peakDate = point.Timestamp;
            }

            if (peakValue <= 0)
                continue;

            decimal drawdownPct =
                (peakValue - point.Equity) / peakValue;

            if (drawdownPct > maxDrawdownPct)
            {
                maxDrawdownPct = drawdownPct;
                worstPeakDate = peakDate;
                worstPeakValue = peakValue;
                worstTroughDate = point.Timestamp;
                worstTroughValue = point.Equity;
            }

        }

        return new DrawdownAuditResult(
            MaxDrawdownPct: maxDrawdownPct,
            PeakDate: worstPeakDate,
            PeakValue: worstPeakValue,
            TroughDate: worstTroughDate,
            TroughValue: worstTroughValue);

    }
}

public sealed record DrawdownAuditResult(
    decimal MaxDrawdownPct,
    DateTime PeakDate,
    decimal PeakValue,
    DateTime TroughDate,
    decimal TroughValue);