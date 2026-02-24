using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

    public static IResult ToServiceUnavailableResult(HttpContext httpContext, string code, string message)
    {
        return Results.Json(
            new ApiErrorResponse(new ApiErrorEnvelope(code, message, httpContext.TraceIdentifier)),
            statusCode: StatusCodes.Status503ServiceUnavailable);
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

public sealed record PlaidLinkTokenIssuedDto(
    Guid LinkSessionId,
    string LinkToken,
    DateTime ExpiresAtUtc,
    string Environment,
    IReadOnlyList<string> Products,
    bool OAuthEnabled,
    string? RedirectUri);

public sealed record PlaidLinkSessionEventLoggedDto(
    Guid LinkSessionId,
    string EventType,
    DateTime LoggedAtUtc);

public sealed record PlaidPublicTokenExchangeResultDto(
    Guid CredentialId,
    Guid? LinkSessionId,
    string ItemId,
    string Environment,
    string Status,
    string? InstitutionId,
    DateTime StoredAtUtc);

public sealed record PlaidItemRecoveryWebhookProcessedDto(
    Guid CredentialId,
    Guid? LinkSessionId,
    string ItemId,
    string Environment,
    string CredentialStatus,
    string? SessionStatus,
    string RecoveryAction,
    string RecoveryReasonCode,
    DateTime ProcessedAtUtc);

public sealed record PlaidTransactionsWebhookProcessedDto(
    Guid SyncStateId,
    string ItemId,
    string Environment,
    string Cursor,
    string SyncStatus,
    int PendingWebhookCount,
    bool InitialUpdateComplete,
    bool HistoricalUpdateComplete,
    DateTime ProcessedAtUtc,
    DateTime? LastWebhookAtUtc,
    string? LastProviderRequestId);

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
    int DueWindowDaysBefore,
    int DueWindowDaysAfter,
    decimal AmountVariancePercent,
    decimal AmountVarianceAbsolute,
    decimal DeterministicMatchThreshold,
    decimal DueDateScoreWeight,
    decimal AmountScoreWeight,
    decimal RecencyScoreWeight,
    string DeterministicScoreVersion,
    string TieBreakPolicy,
    bool IsActive,
    string? UserNote,
    string? AgentNote);

public sealed record ReimbursementProposalDto(
    Guid Id,
    Guid IncomingTransactionId,
    Guid? RelatedTransactionId,
    Guid? RelatedTransactionSplitId,
    decimal ProposedAmount,
    Guid LifecycleGroupId,
    int LifecycleOrdinal,
    string Status,
    string StatusReasonCode,
    string StatusRationale,
    string ProposalSource,
    string ProvenanceSource,
    string? ProvenanceReference,
    string? ProvenancePayloadJson,
    Guid? SupersedesProposalId,
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

    [Range(0, 90)]
    public int DueWindowDaysBefore { get; init; } = 3;

    [Range(0, 90)]
    public int DueWindowDaysAfter { get; init; } = 3;

    [Range(0, 100)]
    public decimal AmountVariancePercent { get; init; } = 5.00m;

    [Range(0, double.MaxValue)]
    public decimal AmountVarianceAbsolute { get; init; }

    [Range(0, 1)]
    public decimal DeterministicMatchThreshold { get; init; } = 0.7000m;

    [Range(0, 1)]
    public decimal DueDateScoreWeight { get; init; } = 0.5000m;

    [Range(0, 1)]
    public decimal AmountScoreWeight { get; init; } = 0.3500m;

    [Range(0, 1)]
    public decimal RecencyScoreWeight { get; init; } = 0.1500m;

    [Required]
    [MaxLength(120)]
    public string DeterministicScoreVersion { get; init; } = "mm-be-07a-v1";

    [Required]
    [MaxLength(240)]
    public string TieBreakPolicy { get; init; } = "due_date_distance_then_amount_delta_then_latest_observed";

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

public sealed class CreatePlaidLinkTokenRequest
{
    public Guid? HouseholdId { get; init; }

    [Required]
    [MaxLength(200)]
    public string ClientUserId { get; init; } = string.Empty;

    [MaxLength(500)]
    [Url]
    public string? RedirectUri { get; init; }

    [MaxLength(4000)]
    public string? ClientMetadataJson { get; init; }

    public IReadOnlyList<string>? Products { get; init; }
}

public sealed class LogPlaidLinkSessionEventRequest
{
    [Required]
    [MaxLength(80)]
    public string EventType { get; init; } = string.Empty;

    [MaxLength(32)]
    public string? Source { get; init; }

    [MaxLength(4000)]
    public string? ClientMetadataJson { get; init; }
}

public sealed class ExchangePlaidPublicTokenRequest
{
    public Guid? HouseholdId { get; init; }

    public Guid? LinkSessionId { get; init; }

    [Required]
    [MaxLength(512)]
    public string PublicToken { get; init; } = string.Empty;

    [MaxLength(128)]
    public string? InstitutionId { get; init; }

    [MaxLength(4000)]
    public string? ClientMetadataJson { get; init; }
}

public sealed class PlaidItemRecoveryWebhookRequest
{
    [Required]
    [MaxLength(80)]
    public string WebhookType { get; init; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string WebhookCode { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string ItemId { get; init; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Environment { get; init; } = string.Empty;

    [MaxLength(120)]
    public string? ProviderRequestId { get; init; }

    [MaxLength(64)]
    public string? ErrorCode { get; init; }

    [MaxLength(128)]
    public string? ErrorType { get; init; }

    [MaxLength(4000)]
    public string? MetadataJson { get; init; }
}

public sealed class PlaidTransactionsWebhookRequest
{
    [Required]
    [MaxLength(80)]
    [JsonPropertyName("webhook_type")]
    public string WebhookType { get; init; } = string.Empty;

    [Required]
    [MaxLength(80)]
    [JsonPropertyName("webhook_code")]
    public string WebhookCode { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [JsonPropertyName("item_id")]
    public string ItemId { get; init; } = string.Empty;

    [Required]
    [MaxLength(32)]
    [JsonPropertyName("environment")]
    public string Environment { get; init; } = string.Empty;

    [MaxLength(500)]
    [JsonPropertyName("cursor")]
    public string? Cursor { get; init; }

    [MaxLength(120)]
    [JsonPropertyName("request_id")]
    public string? ProviderRequestId { get; init; }

    [JsonPropertyName("initial_update_complete")]
    public bool? InitialUpdateComplete { get; init; }

    [JsonPropertyName("historical_update_complete")]
    public bool? HistoricalUpdateComplete { get; init; }
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

    [Range(0, 90)]
    public int? DueWindowDaysBefore { get; init; }

    [Range(0, 90)]
    public int? DueWindowDaysAfter { get; init; }

    [Range(0, 100)]
    public decimal? AmountVariancePercent { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? AmountVarianceAbsolute { get; init; }

    [Range(0, 1)]
    public decimal? DeterministicMatchThreshold { get; init; }

    [Range(0, 1)]
    public decimal? DueDateScoreWeight { get; init; }

    [Range(0, 1)]
    public decimal? AmountScoreWeight { get; init; }

    [Range(0, 1)]
    public decimal? RecencyScoreWeight { get; init; }

    [MaxLength(120)]
    public string? DeterministicScoreVersion { get; init; }

    [MaxLength(240)]
    public string? TieBreakPolicy { get; init; }

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

    public Guid? LifecycleGroupId { get; init; }

    [Range(1, int.MaxValue)]
    public int? LifecycleOrdinal { get; init; }

    [Required]
    [MaxLength(120)]
    public string StatusReasonCode { get; init; } = "proposal_created";

    [Required]
    [MaxLength(500)]
    public string StatusRationale { get; init; } = "Proposal created and awaiting human review.";

    [Required]
    public string ProposalSource { get; init; } = "Deterministic";

    [Required]
    [MaxLength(120)]
    public string ProvenanceSource { get; init; } = "api";

    [MaxLength(200)]
    public string? ProvenanceReference { get; init; }

    public string? ProvenancePayloadJson { get; init; }

    public Guid? SupersedesProposalId { get; init; }

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

public sealed class CreateHouseholdRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;
}

public sealed record HouseholdDto(Guid Id, string Name, DateTime CreatedAtUtc);
