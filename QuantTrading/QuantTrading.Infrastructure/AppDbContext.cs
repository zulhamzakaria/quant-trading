using Microsoft.EntityFrameworkCore;
using QuantTrading.Domain.Entities;
using QuantTrading.Domain.Models;

namespace QuantTrading.Infrastructure;

public sealed class AppDbContext : DbContext
{
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<BacktestResult> BacktestResults => Set<BacktestResult>();
    public DbSet<TradeRecord> TradeRecords => Set<TradeRecord>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        modelBuilder.Entity<TradeRecord>(entity =>
        {
            entity.HasKey(tr => tr.Id);
            entity.HasIndex(tr => tr.BacktestResultId);
            entity.HasOne<BacktestResult>()
            .WithMany()
            .HasForeignKey(tr => tr.BacktestResultId)
            .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BacktestResult>(entity =>
        {
            entity.HasKey(br => br.Id);
            entity.OwnsOne(e => e.InitialCapital, cm =>
            {
                cm.Property(p => p.Amount)
                .HasColumnName("InitialCapitalAmount")
                .HasPrecision(18, 4);
                cm.Property(p => p.Currency)
                .HasColumnName("InitialCapitalCurrency")
                .HasMaxLength(3);
            });
            entity.OwnsOne(e => e.FinalCapital, cm =>
            {
                cm.Property(p => p.Amount)
                .HasColumnName("FinalCapitalAmount")
                .HasPrecision(18, 4);
                cm.Property(p => p.Currency)
                .HasColumnName("FinalCapitalCurrency")
                .HasMaxLength(3);
            });
            entity.OwnsOne(e => e.GrossProfit, cm =>
            {
                cm.Property(p => p.Amount)
                .HasColumnName("GrossProfitAmount")
                .HasPrecision(18, 4);
                cm.Property(p => p.Currency)
                .HasColumnName("GrossProfitCurrency")
                .HasMaxLength(3);
            });
            entity.OwnsOne(e => e.GrossLoss, cm =>
            {
                cm.Property(p => p.Amount)
                .HasColumnName("GrossLossAmount")
                .HasPrecision(18, 4);
                cm.Property(p => p.Currency)
                .HasColumnName("GrossLossCurrency")
                .HasMaxLength(3);
            });
            entity.OwnsMany(e => e.EquityCurve, builder => { builder.ToJson(); });
        });
    }
}
