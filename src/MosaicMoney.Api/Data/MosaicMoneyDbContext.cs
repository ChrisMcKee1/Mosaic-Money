using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Domain.Ledger;

namespace MosaicMoney.Api.Data;

public sealed class MosaicMoneyDbContext : DbContext
{
    public MosaicMoneyDbContext(DbContextOptions<MosaicMoneyDbContext> options)
        : base(options)
    {
    }

    public DbSet<Household> Households => Set<Household>();

    public DbSet<HouseholdUser> HouseholdUsers => Set<HouseholdUser>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Subcategory> Subcategories => Set<Subcategory>();

    public DbSet<RecurringItem> RecurringItems => Set<RecurringItem>();

    public DbSet<EnrichedTransaction> EnrichedTransactions => Set<EnrichedTransaction>();

    public DbSet<TransactionSplit> TransactionSplits => Set<TransactionSplit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<Subcategory>()
            .HasIndex(x => new { x.CategoryId, x.Name })
            .IsUnique();

        modelBuilder.Entity<HouseholdUser>()
            .HasIndex(x => new { x.HouseholdId, x.ExternalUserKey })
            .IsUnique();

        modelBuilder.Entity<Account>()
            .HasIndex(x => new { x.HouseholdId, x.ExternalAccountKey })
            .IsUnique();

        modelBuilder.Entity<EnrichedTransaction>()
            .HasIndex(x => x.PlaidTransactionId)
            .IsUnique();

        modelBuilder.Entity<EnrichedTransaction>()
            .HasIndex(x => new { x.ReviewStatus, x.TransactionDate });

        modelBuilder.Entity<Household>()
            .HasMany(x => x.Users)
            .WithOne(x => x.Household)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Household>()
            .HasMany(x => x.Accounts)
            .WithOne(x => x.Household)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Household>()
            .HasMany(x => x.RecurringItems)
            .WithOne(x => x.Household)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Category>()
            .HasMany(x => x.Subcategories)
            .WithOne(x => x.Category)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Account>()
            .HasMany(x => x.Transactions)
            .WithOne(x => x.Account)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecurringItem>()
            .HasMany(x => x.Transactions)
            .WithOne(x => x.RecurringItem)
            .HasForeignKey(x => x.RecurringItemId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Subcategory>()
            .HasMany(x => x.Transactions)
            .WithOne(x => x.Subcategory)
            .HasForeignKey(x => x.SubcategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<HouseholdUser>()
            .HasMany(x => x.NeedsReviewTransactions)
            .WithOne(x => x.NeedsReviewByUser)
            .HasForeignKey(x => x.NeedsReviewByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<EnrichedTransaction>()
            .HasMany(x => x.Splits)
            .WithOne(x => x.ParentTransaction)
            .HasForeignKey(x => x.ParentTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Subcategory>()
            .HasMany(x => x.Splits)
            .WithOne(x => x.Subcategory)
            .HasForeignKey(x => x.SubcategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
