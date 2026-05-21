using QuantTrading.Domain.Entities;
using QuantTrading.Domain.Models;

namespace QuantTrading.Application.Interfaces;

public interface IRiskRule
{
    bool Allows(Signal signal, Portfolio portfolio, out string rejection);
}
