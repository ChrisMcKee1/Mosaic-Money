using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Contracts.V1;

public sealed record ApiErrorResponse(ApiErrorEnvelope Error);

public sealed record ApiErrorEnvelope(
    string Code,
    string Message,
    string TraceId,
    IReadOnlyList<ApiValidationError>? Details = null);

public sealed record ApiValidationError(string Field, string Message);

public static class ApiValidation
{
    public static IReadOnlyList<ApiValidationError> ValidateDataAnnotations<T>(T model)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(model!, new ValidationContext(model!), validationResults, validateAllProperties: true);

        return validationResults
            .SelectMany(result =>
            {
                var members = result.MemberNames.Any() ? result.MemberNames : [string.Empty];
                return members.Select(member => new ApiValidationError(member, result.ErrorMessage ?? "Invalid value."));
            })
            .ToList();
    }

    public static IResult ToValidationResult(HttpContext httpContext, IEnumerable<ApiValidationError> errors)
    {
        var details = errors.ToList();
        return Results.BadRequest(new ApiErrorResponse(
            new ApiErrorEnvelope(
                "validation_failed",
                "One or more validation errors occurred.",
                httpContext.TraceIdentifier,
                details)));
    }

    public static IResult ToNotFoundResult(HttpContext httpContext, string code, string message)
    {
        return Results.NotFound(new ApiErrorResponse(
            new ApiErrorEnvelope(code, message, httpContext.TraceIdentifier)));
    }

    public static IResult ToConflictResult(HttpContext httpContext, string code, string message)
    {
        return Results.Conflict(new ApiErrorResponse(
            new ApiErrorEnvelope(code, message, httpContext.TraceIdentifier)));
    }
}

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

public sealed record PlaidDeltaIngestionItemResultDto(
    string PlaidTransactionId,
    Guid EnrichedTransactionId,
    bool RawDuplicate,
    string Disposition,
    string ReviewStatus,
    string? ReviewReason);

public sealed record PlaidDeltaIngestionResultDto(
    int RawStoredCount,
    int RawDuplicateCount,
    int InsertedCount,
    int UpdatedCount,
    int UnchangedCount,
    IReadOnlyList<PlaidDeltaIngestionItemResultDto> Items);

public sealed record ClassificationStageOutputDto(
    Guid Id,
    string Stage,
    int StageOrder,
    Guid? ProposedSubcategoryId,
    decimal Confidence,
    string RationaleCode,
    string Rationale,
    bool EscalatedToNextStage,
    DateTime ProducedAtUtc);

public sealed record ClassificationOutcomeDto(
    Guid Id,
    Guid TransactionId,
    Guid? ProposedSubcategoryId,
    decimal FinalConfidence,
    string Decision,
    string ReviewStatus,
    string DecisionReasonCode,
    string DecisionRationale,
    string? AgentNoteSummary,
    DateTime CreatedAtUtc,
    IReadOnlyList<ClassificationStageOutputDto> StageOutputs);

public sealed record RecurringItemDto(
    Guid Id,
    Guid HouseholdId,
    string MerchantName,
    decimal ExpectedAmount,
    bool IsVariable,
    string Frequency,
    DateOnly NextDueDate,
    bool IsActive,
    string? UserNote,
    string? AgentNote);

public sealed record ReimbursementProposalDto(
    Guid Id,
    Guid IncomingTransactionId,
    Guid? RelatedTransactionId,
    Guid? RelatedTransactionSplitId,
    decimal ProposedAmount,
    string Status,
    Guid? DecisionedByUserId,
    DateTime? DecisionedAtUtc,
    string? UserNote,
    string? AgentNote,
    DateTime CreatedAtUtc);

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

public sealed class CreateRecurringItemRequest
{
    [Required]
    public Guid HouseholdId { get; init; }

    [Required]
    [MaxLength(200)]
    public string MerchantName { get; init; } = string.Empty;

    public decimal ExpectedAmount { get; init; }

    public bool IsVariable { get; init; }

    [Required]
    public string Frequency { get; init; } = "Monthly";

    public DateOnly NextDueDate { get; init; }

    public bool IsActive { get; init; } = true;

    public string? UserNote { get; init; }

    public string? AgentNote { get; init; }
}

public sealed class IngestPlaidDeltaTransactionRequest
{
    [Required]
    [MaxLength(128)]
    public string PlaidTransactionId { get; init; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Description { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateOnly TransactionDate { get; init; }

    [Required]
    public string RawPayloadJson { get; init; } = string.Empty;

    public bool IsAmbiguous { get; init; }

    [MaxLength(300)]
    public string? ReviewReason { get; init; }
}

public sealed class IngestPlaidDeltaRequest
{
    [Required]
    public Guid AccountId { get; init; }

    [Required]
    [MaxLength(200)]
    public string DeltaCursor { get; init; } = string.Empty;

    [Required]
    public IReadOnlyList<IngestPlaidDeltaTransactionRequest> Transactions { get; init; } = [];
}

public sealed class CreateClassificationStageOutputRequest
{
    [Required]
    public string Stage { get; init; } = string.Empty;

    [Range(1, 3)]
    public int StageOrder { get; init; }

    public Guid? ProposedSubcategoryId { get; init; }

    [Range(0, 1)]
    public decimal Confidence { get; init; }

    [Required]
    [MaxLength(120)]
    public string RationaleCode { get; init; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Rationale { get; init; } = string.Empty;

    public bool EscalatedToNextStage { get; init; }
}

public sealed class CreateClassificationOutcomeRequest
{
    public Guid? ProposedSubcategoryId { get; init; }

    [Range(0, 1)]
    public decimal FinalConfidence { get; init; }

    [Required]
    public string Decision { get; init; } = string.Empty;

    [Required]
    public string ReviewStatus { get; init; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string DecisionReasonCode { get; init; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string DecisionRationale { get; init; } = string.Empty;

    // This contract intentionally allows summary-only notes and excludes raw transcripts.
    [MaxLength(600)]
    public string? AgentNoteSummary { get; init; }

    [Required]
    public IReadOnlyList<CreateClassificationStageOutputRequest> StageOutputs { get; init; } = [];
}

public sealed class UpdateRecurringItemRequest
{
    [MaxLength(200)]
    public string? MerchantName { get; init; }

    public decimal? ExpectedAmount { get; init; }

    public bool? IsVariable { get; init; }

    public string? Frequency { get; init; }

    public DateOnly? NextDueDate { get; init; }

    public bool? IsActive { get; init; }

    public string? UserNote { get; init; }

    public string? AgentNote { get; init; }
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

public sealed class CreateReimbursementProposalRequest
{
    [Required]
    public Guid IncomingTransactionId { get; init; }

    public Guid? RelatedTransactionId { get; init; }

    public Guid? RelatedTransactionSplitId { get; init; }

    public decimal ProposedAmount { get; init; }

    public string? UserNote { get; init; }

    public string? AgentNote { get; init; }
}

public sealed class ReimbursementDecisionRequest
{
    [Required]
    public string Action { get; init; } = string.Empty;

    [Required]
    public Guid DecisionedByUserId { get; init; }

    public string? UserNote { get; init; }

    public string? AgentNote { get; init; }
}
