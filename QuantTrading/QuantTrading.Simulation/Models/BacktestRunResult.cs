using QuantTrading.Domain.Models;
using QuantTrading.Domain.ValueObjects;
using QuantTrading.Simulation.Execution;

namespace QuantTrading.Simulation.Models;

public sealed record BacktestRunResult(
    string StrategyName,
    Money InitialCapital,
    Money FinalPortfolioValue,
    IReadOnlyCollection<EquityCurvePoint> EquityCurve,
    IReadOnlyCollection<FillReceipt> Fills)
{
    public decimal TotalReturnPercentage 
        => (FinalPortfolioValue.Amount - InitialCapital.Amount) / InitialCapital.Amount * 100m;
};


