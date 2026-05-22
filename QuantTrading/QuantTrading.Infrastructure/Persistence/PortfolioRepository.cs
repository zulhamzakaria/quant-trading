using Microsoft.EntityFrameworkCore;
using QuantTrading.Application.Interfaces;
using QuantTrading.Domain.Entities;

namespace QuantTrading.Infrastructure.Persistence;

public class PortfolioRepository : IPortfolioRepository
{
    private readonly AppDbContext _context;
    public PortfolioRepository(AppDbContext context)
        => _context = context;
    public async Task<Portfolio?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Portfolios
            .Include(p => p.OpenPositions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public void Add(Portfolio portfolio)
        => _context.Portfolios.Add(portfolio);

    public void Delete(Portfolio portfolio)
        => _context.Portfolios.Remove(portfolio);

    public void Update(Portfolio portfolio)
        => _context.Portfolios.Update(portfolio);
}
