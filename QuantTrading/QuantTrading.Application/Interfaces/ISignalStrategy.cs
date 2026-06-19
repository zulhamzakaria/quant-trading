using QuantTrading.Domain.Models;
using QuantTrading.Shared.Models;

namespace QuantTrading.Application.Interfaces;

public interface ISignalStrategy
{
    Signal Update(MarketData data);
}
