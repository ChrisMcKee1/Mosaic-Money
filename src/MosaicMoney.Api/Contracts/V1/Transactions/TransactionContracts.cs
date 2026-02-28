using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Contracts.V1;

public sealed record TransactionSplitDto(
    Guid Id,
    Guid? SubcategoryId,
    decimal Amount,
    int AmortizationMonths,
    string? UserNote,
    string? AgentNote);

public sealed record TransactionDto(
    Guid Id,
    Guid AccountId,
    Guid? RecurringItemId,
    Guid? SubcategoryId,
    Guid? NeedsReviewByUserId,
    string? PlaidTransactionId,
    string Description,
    decimal Amount,
    DateOnly TransactionDate,
    string ReviewStatus,
    string? ReviewReason,
    bool ExcludeFromBudget,
    bool IsExtraPrincipal,
    string? UserNote,
    string? AgentNote,
    IReadOnlyList<TransactionSplitDto> Splits,
    DateTime CreatedAtUtc,
    DateTime LastModifiedAtUtc);

public sealed record TransactionSplitProjectionMetadataDto(
    Guid Id,
    Guid? SubcategoryId,
    decimal RawAmount,
    int AmortizationMonths);

public sealed record RecurringProjectionMetadataDto(
    bool IsLinked,
    Guid? RecurringItemId,
    bool? IsActive,
    string? Frequency,
    DateOnly? NextDueDate);

public sealed record ReimbursementProjectionMetadataDto(
    bool HasProposals,
    int ProposalCount,
    bool HasPendingHumanReview,
    string? LatestStatus,
    string? LatestStatusReasonCode,
    decimal PendingOrNeedsReviewAmount,
    decimal ApprovedAmount);

public sealed record TransactionProjectionMetadataDto(
    Guid Id,
    Guid AccountId,
    string Description,
    decimal RawAmount,
    DateOnly RawTransactionDate,
    string ReviewStatus,
    string? ReviewReason,
    bool ExcludeFromBudget,
    bool IsExtraPrincipal,
    RecurringProjectionMetadataDto Recurring,
    ReimbursementProjectionMetadataDto Reimbursement,
    IReadOnlyList<TransactionSplitProjectionMetadataDto> Splits,
    DateTime CreatedAtUtc,
    DateTime LastModifiedAtUtc);

public sealed class CreateTransactionSplitRequest
{
    public Guid? SubcategoryId { get; init; }

    public decimal Amount { get; init; }

    [Range(1, 240)]
    public int AmortizationMonths { get; init; } = 1;

    public string? UserNote { get; init; }

    public string? AgentNote { get; init; }
}

public sealed class CreateTransactionRequest
{
    [Required]
    public Guid AccountId { get; init; }

    public Guid? RecurringItemId { get; init; }

    public Guid? SubcategoryId { get; init; }

    public Guid? NeedsReviewByUserId { get; init; }

    [MaxLength(128)]
    public string? PlaidTransactionId { get; init; }

    [Required]
    [MaxLength(500)]
    public string Description { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateOnly TransactionDate { get; init; }

    public string ReviewStatus { get; init; } = "None";

    [MaxLength(300)]
    public string? ReviewReason { get; init; }

    public bool ExcludeFromBudget { get; init; }

    public bool IsExtraPrincipal { get; init; }

    public string? UserNote { get; init; }

    public string? AgentNote { get; init; }

    public IReadOnlyList<CreateTransactionSplitRequest> Splits { get; init; } = [];
}

public sealed class ReviewActionRequest
{
    [Required]
    public Guid TransactionId { get; init; }

    [Required]
    public string Action { get; init; } = string.Empty;

    public Guid? SubcategoryId { get; init; }

    [MaxLength(300)]
    public string? ReviewReason { get; init; }

    public Guid? NeedsReviewByUserId { get; init; }

    public string? UserNote { get; init; }

    public string? AgentNote { get; init; }

    public bool? ExcludeFromBudget { get; init; }

    public bool? IsExtraPrincipal { get; init; }
}