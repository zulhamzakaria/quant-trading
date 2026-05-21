using QuantTrading.Application.Interfaces;
using QuantTrading.Domain.Entities;
using QuantTrading.Domain.Models;

namespace QuantTrading.Infrastructure.RiskManagement;

public sealed class SufficientCashRule : IRiskRule
{
    public bool Allows(Signal signal, Portfolio portfolio, out string rejection)
    {
        decimal requiredCash = signal.Quantity * signal.Price;
        if (portfolio.Cash < requiredCash)
        {
            rejection = $"Insufficient cash. Required: {requiredCash}, Available: {portfolio.Cash}.";
            return false;
        }

        rejection = string.Empty;
        return true;
    }
}
