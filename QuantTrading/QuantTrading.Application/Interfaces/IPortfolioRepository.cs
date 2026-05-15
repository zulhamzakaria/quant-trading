using QuantTrading.Domain.Entities;

namespace QuantTrading.Application.Interfaces;

public interface IPortfolioRepository
{
    Task<Portfolio?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(Portfolio portfolio);
    void Update(Portfolio portfolio);
    void Delete(Portfolio portfolio);
}
