using QuantTrading.Application.Interfaces;

namespace QuantTrading.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
        => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) 
        => _context.SaveChangesAsync(ct);
}
