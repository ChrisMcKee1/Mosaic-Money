using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MosaicMoney.Api.Domain.Ledger;

public enum TransactionReviewStatus
{
    None = 0,
    NeedsReview = 1,
    Reviewed = 2,
}

public enum RecurringFrequency
{
    Weekly = 1,
    BiWeekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Annually = 5,
}

public sealed class Household
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<HouseholdUser> Users { get; set; } = new List<HouseholdUser>();

    public ICollection<Account> Accounts { get; set; } = new List<Account>();

    public ICollection<RecurringItem> RecurringItems { get; set; } = new List<RecurringItem>();
}

public sealed class HouseholdUser
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }

    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ExternalUserKey { get; set; }

    public Household Household { get; set; } = null!;

    public ICollection<EnrichedTransaction> NeedsReviewTransactions { get; set; } = new List<EnrichedTransaction>();
}

public sealed class Account
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? InstitutionName { get; set; }

    [MaxLength(128)]
    public string? ExternalAccountKey { get; set; }

    public bool IsActive { get; set; } = true;

    public Household Household { get; set; } = null!;

    public ICollection<EnrichedTransaction> Transactions { get; set; } = new List<EnrichedTransaction>();
}

public sealed class Category
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsSystem { get; set; }

    public ICollection<Subcategory> Subcategories { get; set; } = new List<Subcategory>();
}

public sealed class Subcategory
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public bool IsBusinessExpense { get; set; }

    public Category Category { get; set; } = null!;

    public ICollection<EnrichedTransaction> Transactions { get; set; } = new List<EnrichedTransaction>();

    public ICollection<TransactionSplit> Splits { get; set; } = new List<TransactionSplit>();
}

public sealed class RecurringItem
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }

    [MaxLength(200)]
    public string MerchantName { get; set; } = string.Empty;

    [Precision(18, 2)]
    public decimal ExpectedAmount { get; set; }

    public bool IsVariable { get; set; }

    public RecurringFrequency Frequency { get; set; } = RecurringFrequency.Monthly;

    public DateOnly NextDueDate { get; set; }

    public bool IsActive { get; set; } = true;

    public string? UserNote { get; set; }

    public string? AgentNote { get; set; }

    public Household Household { get; set; } = null!;

    public ICollection<EnrichedTransaction> Transactions { get; set; } = new List<EnrichedTransaction>();
}

public sealed class EnrichedTransaction
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Guid? RecurringItemId { get; set; }

    public Guid? SubcategoryId { get; set; }

    public Guid? NeedsReviewByUserId { get; set; }

    [MaxLength(128)]
    public string? PlaidTransactionId { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    // Single-entry ledger semantics: one signed amount field per transaction.
    [Precision(18, 2)]
    public decimal Amount { get; set; }

    public DateOnly TransactionDate { get; set; }

    public TransactionReviewStatus ReviewStatus { get; set; }

    [MaxLength(300)]
    public string? ReviewReason { get; set; }

    public bool ExcludeFromBudget { get; set; }

    public bool IsExtraPrincipal { get; set; }

    public string? UserNote { get; set; }

    public string? AgentNote { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedAtUtc { get; set; } = DateTime.UtcNow;

    public Account Account { get; set; } = null!;

    public RecurringItem? RecurringItem { get; set; }

    public Subcategory? Subcategory { get; set; }

    public HouseholdUser? NeedsReviewByUser { get; set; }

    public ICollection<TransactionSplit> Splits { get; set; } = new List<TransactionSplit>();
}

public sealed class TransactionSplit
{
    public Guid Id { get; set; }

    public Guid ParentTransactionId { get; set; }

    public Guid? SubcategoryId { get; set; }

    // Single-entry split amount for projected allocation and reporting.
    [Precision(18, 2)]
    public decimal Amount { get; set; }

    [Range(1, 240)]
    public int AmortizationMonths { get; set; } = 1;

    public string? UserNote { get; set; }

    public string? AgentNote { get; set; }

    public EnrichedTransaction ParentTransaction { get; set; } = null!;

    public Subcategory? Subcategory { get; set; }
}
