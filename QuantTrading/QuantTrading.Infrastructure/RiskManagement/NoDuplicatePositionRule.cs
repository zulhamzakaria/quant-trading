using QuantTrading.Application.Interfaces;
using QuantTrading.Domain.Entities;
using QuantTrading.Domain.Models;

namespace QuantTrading.Infrastructure.RiskManagement;

public sealed class NoDuplicatePositionRule : IRiskRule
{
    public bool Allows(Signal signal, Portfolio portfolio, out string rejection)
    {
        if (portfolio.OpenPositions.Any(p => p.Symbol == signal.Symbol))
        {
            rejection = $"Duplicate position for symbol {signal.Symbol} is not allowed.";
            return false;
        }

        rejection = string.Empty;
        return true;
    }
}
