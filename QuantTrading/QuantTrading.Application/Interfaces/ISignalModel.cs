using QuantTrading.Application.Models;
using QuantTrading.Domain.Models;

namespace QuantTrading.Application.Interfaces;

public interface ISignalModel
{
    Signal Predict(MarketData data);
}
