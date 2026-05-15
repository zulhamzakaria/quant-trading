using Microsoft.EntityFrameworkCore;
using QuantTrading.Application.Interfaces;
using QuantTrading.Domain.Entities;

namespace QuantTrading.Infrastructure.Persistence;

public class TradeRepository : ITradeRepository
{
    private readonly AppDbContext _context;
    public TradeRepository(AppDbContext context)
        => _context = context;

    public async Task<Trade?> GetByIdAsync
        (Guid id, CancellationToken ct = default)
    {
        return await _context.Trades
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public void Add(Trade trade)
        => _context.Trades.Add(trade);

    public void Update(Trade trade)
        => _context.Trades.Update(trade);

    public void Delete(Trade trade)
        => _context.Trades.Remove(trade);
}