using QuantTrading.Application.Interfaces;
using QuantTrading.Domain.Entities;
using QuantTrading.Domain.Models;

namespace QuantTrading.Infrastructure.RiskManagement;

public sealed class MaxPositionSizingRule : IRiskRule
{
    private readonly decimal _maxPortfolioAllocationPercent;
    public MaxPositionSizingRule(decimal maxPortfolioAllocationPercent = .10m)
    {
        _maxPortfolioAllocationPercent = maxPortfolioAllocationPercent;
    }
    public bool Allows(Signal signal, Portfolio portfolio, out string rejection)
    {

        var currentPrices = new Dictionary<string, decimal>
        {
            {
                signal.Symbol, 
                signal.TargetValue / signal.Confidence
            }
        };

        decimal totalEquity = portfolio.CalculateTotalEquity(currentPrices);

        decimal maxAllowedValue = totalEquity * _maxPortfolioAllocationPercent;
        if(signal.TargetValue > maxAllowedValue)
        {
            rejection = $"Position size of {signal.TargetValue:C} exceeds max allowed allocation of {maxAllowedValue:C} ({_maxPortfolioAllocationPercent:P} of portfolio).";
            return false;
        }
        rejection = string.Empty;
        return true;
    }
}
