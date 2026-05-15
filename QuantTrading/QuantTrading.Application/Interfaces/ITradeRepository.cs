using QuantTrading.Domain.Entities;

namespace QuantTrading.Application.Interfaces;

public interface ITradeRepository
{
    Task<Trade?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(Trade trade);
    void Update(Trade trade);
    void Delete(Trade trade);
}
