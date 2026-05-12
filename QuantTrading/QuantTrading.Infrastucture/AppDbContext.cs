using Microsoft.EntityFrameworkCore;
using QuantTrading.Domain.Entities;

namespace QuantTrading.Infrastructure;

public sealed class AppDbContext : DbContext
{
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    public DbSet<Trade> Trades => Set<Trade>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
