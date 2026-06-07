using QuantTrading.Domain.Models;

namespace QuantTrading.Application.Interfaces;

public interface ISignalStrategy
{
    Signal Update(MarketData data);
}
