using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Pgvector;

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

public enum ReimbursementProposalStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    NeedsReview = 4,
    Superseded = 5,
    Cancelled = 6,
}

public enum ReimbursementProposalSource
{
    Deterministic = 1,
    Manual = 2,
}

public enum ClassificationStage
{
    Deterministic = 1,
    Semantic = 2,
    MafFallback = 3,
}

public enum ClassificationDecision
{
    Categorized = 1,
    NeedsReview = 2,
}

public enum IngestionDisposition
{
    Inserted = 1,
    Updated = 2,
    Unchanged = 3,
}

public enum PlaidLinkSessionStatus
{
    Issued = 1,
    Open = 2,
    Exit = 3,
    Success = 4,
    Exchanged = 5,
    Error = 6,
}

public enum PlaidItemCredentialStatus
{
    Active = 1,
    RequiresRelink = 2,
    Revoked = 3,
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

    public ICollection<TransactionClassificationOutcome> ClassificationOutcomeProposals { get; set; } = new List<TransactionClassificationOutcome>();

    public ICollection<ClassificationStageOutput> ClassificationStageProposals { get; set; } = new List<ClassificationStageOutput>();
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

    [Range(0, 90)]
    public int DueWindowDaysBefore { get; set; } = 3;

    [Range(0, 90)]
    public int DueWindowDaysAfter { get; set; } = 3;

    [Precision(5, 2)]
    public decimal AmountVariancePercent { get; set; } = 5.00m;

    [Precision(18, 2)]
    public decimal AmountVarianceAbsolute { get; set; }

    [Precision(5, 4)]
    public decimal DeterministicMatchThreshold { get; set; } = 0.7000m;

    [Precision(5, 4)]
    public decimal DueDateScoreWeight { get; set; } = 0.5000m;

    [Precision(5, 4)]
    public decimal AmountScoreWeight { get; set; } = 0.3500m;

    [Precision(5, 4)]
    public decimal RecencyScoreWeight { get; set; } = 0.1500m;

    [MaxLength(120)]
    public string DeterministicScoreVersion { get; set; } = "mm-be-07a-v1";

    [MaxLength(240)]
    public string TieBreakPolicy { get; set; } = "due_date_distance_then_amount_delta_then_latest_observed";

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

    // Stores semantic embedding for vector similarity lookup.
    public Vector? DescriptionEmbedding { get; set; }

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

    public ICollection<TransactionClassificationOutcome> ClassificationOutcomes { get; set; } = new List<TransactionClassificationOutcome>();

    public ICollection<RawTransactionIngestionRecord> RawIngestionRecords { get; set; } = new List<RawTransactionIngestionRecord>();

    public ICollection<ReimbursementProposal> ReimbursementProposals { get; set; } = new List<ReimbursementProposal>();
}

public sealed class RawTransactionIngestionRecord
{
    public Guid Id { get; set; }

    [MaxLength(32)]
    public string Source { get; set; } = "plaid";

    [MaxLength(200)]
    public string DeltaCursor { get; set; } = string.Empty;

    public Guid AccountId { get; set; }

    [MaxLength(128)]
    public string SourceTransactionId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string PayloadHash { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastProcessedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? EnrichedTransactionId { get; set; }

    public IngestionDisposition LastDisposition { get; set; } = IngestionDisposition.Inserted;

    [MaxLength(300)]
    public string? LastReviewReason { get; set; }

    public Account Account { get; set; } = null!;

    public EnrichedTransaction? EnrichedTransaction { get; set; }
}

public sealed class PlaidLinkSession
{
    public Guid Id { get; set; }

    public Guid? HouseholdId { get; set; }

    [MaxLength(200)]
    public string ClientUserId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string LinkTokenHash { get; set; } = string.Empty;

    [MaxLength(64)]
    public string OAuthStateId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? RedirectUri { get; set; }

    [MaxLength(500)]
    public string RequestedProducts { get; set; } = string.Empty;

    [MaxLength(32)]
    public string RequestedEnvironment { get; set; } = "sandbox";

    public PlaidLinkSessionStatus Status { get; set; } = PlaidLinkSessionStatus.Issued;

    public DateTime LinkTokenCreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LinkTokenExpiresAtUtc { get; set; }

    public DateTime? LastEventAtUtc { get; set; }

    [MaxLength(120)]
    public string? LastProviderRequestId { get; set; }

    public string? LastClientMetadataJson { get; set; }

    [MaxLength(128)]
    public string? LinkedItemId { get; set; }

    public ICollection<PlaidLinkSessionEvent> Events { get; set; } = new List<PlaidLinkSessionEvent>();
}

public sealed class PlaidLinkSessionEvent
{
    public Guid Id { get; set; }

    public Guid PlaidLinkSessionId { get; set; }

    [MaxLength(80)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Source { get; set; } = "client";

    public string? ClientMetadataJson { get; set; }

    [MaxLength(120)]
    public string? ProviderRequestId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public PlaidLinkSession PlaidLinkSession { get; set; } = null!;
}

public sealed class PlaidItemCredential
{
    public Guid Id { get; set; }

    public Guid? HouseholdId { get; set; }

    [MaxLength(128)]
    public string ItemId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string PlaidEnvironment { get; set; } = "sandbox";

    public string AccessTokenCiphertext { get; set; } = string.Empty;

    [MaxLength(64)]
    public string AccessTokenFingerprint { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? InstitutionId { get; set; }

    public PlaidItemCredentialStatus Status { get; set; } = PlaidItemCredentialStatus.Active;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastRotatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(120)]
    public string? LastProviderRequestId { get; set; }

    public Guid? LastLinkedSessionId { get; set; }

    public string? LastClientMetadataJson { get; set; }
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

public sealed class ReimbursementProposal
{
    public Guid Id { get; set; }

    public Guid IncomingTransactionId { get; set; }

    public Guid? RelatedTransactionId { get; set; }

    public Guid? RelatedTransactionSplitId { get; set; }

    [Precision(18, 2)]
    public decimal ProposedAmount { get; set; }

    public Guid LifecycleGroupId { get; set; }

    [Range(1, int.MaxValue)]
    public int LifecycleOrdinal { get; set; } = 1;

    public ReimbursementProposalStatus Status { get; set; } = ReimbursementProposalStatus.PendingApproval;

    [MaxLength(120)]
    public string StatusReasonCode { get; set; } = "proposal_created";

    [MaxLength(500)]
    public string StatusRationale { get; set; } = "Proposal created and awaiting human review.";

    public ReimbursementProposalSource ProposalSource { get; set; } = ReimbursementProposalSource.Deterministic;

    [MaxLength(120)]
    public string ProvenanceSource { get; set; } = "unknown";

    [MaxLength(200)]
    public string? ProvenanceReference { get; set; }

    public string? ProvenancePayloadJson { get; set; }

    public Guid? SupersedesProposalId { get; set; }

    public Guid? DecisionedByUserId { get; set; }

    public DateTime? DecisionedAtUtc { get; set; }

    public string? UserNote { get; set; }

    public string? AgentNote { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public EnrichedTransaction IncomingTransaction { get; set; } = null!;

    public ReimbursementProposal? SupersedesProposal { get; set; }

    public ICollection<ReimbursementProposal> SupersededByProposals { get; set; } = new List<ReimbursementProposal>();
}

public sealed class TransactionClassificationOutcome
{
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public Guid? ProposedSubcategoryId { get; set; }

    [Precision(5, 4)]
    public decimal FinalConfidence { get; set; }

    public ClassificationDecision Decision { get; set; }

    public TransactionReviewStatus ReviewStatus { get; set; }

    [MaxLength(120)]
    public string DecisionReasonCode { get; set; } = string.Empty;

    [MaxLength(500)]
    public string DecisionRationale { get; set; } = string.Empty;

    // Persist only concise summaries, never raw model transcripts.
    [MaxLength(600)]
    public string? AgentNoteSummary { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public EnrichedTransaction Transaction { get; set; } = null!;

    public Subcategory? ProposedSubcategory { get; set; }

    public ICollection<ClassificationStageOutput> StageOutputs { get; set; } = new List<ClassificationStageOutput>();
}

public sealed class ClassificationStageOutput
{
    public Guid Id { get; set; }

    public Guid OutcomeId { get; set; }

    public ClassificationStage Stage { get; set; }

    [Range(1, 3)]
    public int StageOrder { get; set; }

    public Guid? ProposedSubcategoryId { get; set; }

    [Precision(5, 4)]
    public decimal Confidence { get; set; }

    [MaxLength(120)]
    public string RationaleCode { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Rationale { get; set; } = string.Empty;

    public bool EscalatedToNextStage { get; set; }

    public DateTime ProducedAtUtc { get; set; } = DateTime.UtcNow;

    public TransactionClassificationOutcome Outcome { get; set; } = null!;

    public Subcategory? ProposedSubcategory { get; set; }
}
