using Microsoft.EntityFrameworkCore;
using RewardEngine.Application;
using RewardEngine.Domain;

namespace RewardEngine.Infrastructure;

public sealed class RewardEngineDbContext(DbContextOptions<RewardEngineDbContext> options) : DbContext(options), IRewardEngineDbContext
{
    public DbSet<User> UserSet => Set<User>();
    public DbSet<Transaction> TransactionSet => Set<Transaction>();
    public DbSet<CashbackRule> CashbackRuleSet => Set<CashbackRule>();
    public DbSet<CashbackReward> CashbackRewardSet => Set<CashbackReward>();
    public DbSet<ProcessedEvent> ProcessedEventSet => Set<ProcessedEvent>();
    public DbSet<OutboxMessage> OutboxMessageSet => Set<OutboxMessage>();
    IQueryable<User> IRewardEngineDbContext.Users => UserSet;
    IQueryable<Transaction> IRewardEngineDbContext.Transactions => TransactionSet;
    IQueryable<CashbackRule> IRewardEngineDbContext.CashbackRules => CashbackRuleSet;
    IQueryable<CashbackReward> IRewardEngineDbContext.CashbackRewards => CashbackRewardSet;
    IQueryable<ProcessedEvent> IRewardEngineDbContext.ProcessedEvents => ProcessedEventSet;
    IQueryable<OutboxMessage> IRewardEngineDbContext.OutboxMessages => OutboxMessageSet;
    void IRewardEngineDbContext.Add<TEntity>(TEntity entity) => Add(entity);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is not null)
        {
            await action(cancellationToken);
            return;
        }
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b => { b.ToTable("Users"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200).IsRequired(); });
        modelBuilder.Entity<Transaction>(b =>
        {
            b.ToTable("Transactions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Merchant).HasMaxLength(300).IsRequired();
            b.Property(x => x.Category).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.CreatedAt);
            b.HasOne(x => x.User).WithMany(x => x.Transactions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CashbackRule>(b =>
        {
            b.ToTable("CashbackRules");
            b.HasKey(x => x.Id);
            b.Property(x => x.Category).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Percentage).HasPrecision(5, 2);
            b.Property(x => x.MonthlyLimit).HasPrecision(18, 2);
            b.HasIndex(x => x.Category).IsUnique();
            b.ToTable(t => { t.HasCheckConstraint("CK_CashbackRules_Percentage", "\"Percentage\" >= 0 AND \"Percentage\" <= 100"); t.HasCheckConstraint("CK_CashbackRules_MonthlyLimit", "\"MonthlyLimit\" >= 0"); });
        });
        modelBuilder.Entity<CashbackReward>(b =>
        {
            b.ToTable("CashbackRewards");
            b.HasKey(x => x.Id);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Percentage).HasPrecision(5, 2);
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.TransactionId).IsUnique();
            b.HasOne(x => x.Transaction).WithOne(x => x.Reward).HasForeignKey<CashbackReward>(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ProcessedEvent>(b => { b.ToTable("ProcessedEvents"); b.HasKey(x => x.EventId); b.HasIndex(x => x.EventId).IsUnique(); });
        modelBuilder.Entity<OutboxMessage>(b => { b.ToTable("OutboxMessages"); b.HasKey(x => x.Id); b.Property(x => x.EventType).HasMaxLength(200); b.Property(x => x.Payload).HasColumnType("jsonb"); b.Property(x => x.LastError).HasMaxLength(2000); b.HasIndex(x => x.ProcessedAt); b.HasIndex(x => x.NextAttemptAt); });

        var created = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        modelBuilder.Entity<CashbackRule>().HasData(
            new CashbackRule { Id = 1, Category = TransactionCategory.Other, Percentage = 1m, MonthlyLimit = 1000m, CreatedAt = created, UpdatedAt = created },
            new CashbackRule { Id = 2, Category = TransactionCategory.Restaurants, Percentage = 5m, MonthlyLimit = 3000m, CreatedAt = created, UpdatedAt = created },
            new CashbackRule { Id = 3, Category = TransactionCategory.Taxi, Percentage = 10m, MonthlyLimit = 2000m, CreatedAt = created, UpdatedAt = created },
            new CashbackRule { Id = 4, Category = TransactionCategory.Fuel, Percentage = 7m, MonthlyLimit = 2500m, CreatedAt = created, UpdatedAt = created },
            new CashbackRule { Id = 5, Category = TransactionCategory.Groceries, Percentage = 3m, MonthlyLimit = 2000m, CreatedAt = created, UpdatedAt = created },
            new CashbackRule { Id = 6, Category = TransactionCategory.Entertainment, Percentage = 5m, MonthlyLimit = 1500m, CreatedAt = created, UpdatedAt = created });
    }
}
