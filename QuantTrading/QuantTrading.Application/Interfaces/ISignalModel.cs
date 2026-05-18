using QuantTrading.Domain.Models;

namespace QuantTrading.Application.Interfaces;

public interface ISignalModel
{
    Signal Predict(IMarketData data);
}
