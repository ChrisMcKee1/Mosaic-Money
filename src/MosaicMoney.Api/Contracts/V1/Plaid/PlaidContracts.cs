using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MosaicMoney.Api.Contracts.V1;

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

public sealed record NetWorthHistoryPointDto(
    DateTime Date,
    decimal DepositoryBalance,
    decimal InvestmentBalance,
    decimal LiabilityBalance,
    decimal NetWorth);

public sealed record LiabilitySnapshotDto(
    Guid Id,
    string LiabilityType,
    DateOnly? AsOfDate,
    decimal? CurrentBalance,
    decimal? LastStatementBalance,
    decimal? MinimumPayment,
    decimal? LastPaymentAmount,
    DateOnly? LastPaymentDate,
    DateOnly? NextPaymentDueDate,
    decimal? Apr,
    DateTime CapturedAtUtc,
    string? ProviderRequestId);

public sealed record LiabilityAccountDto(
    Guid Id,
    Guid? HouseholdId,
    string ItemId,
    string Environment,
    string PlaidAccountId,
    string Name,
    string? OfficialName,
    string? Mask,
    string? AccountType,
    string? AccountSubtype,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc,
    string? LastProviderRequestId,
    IReadOnlyList<LiabilitySnapshotDto> Snapshots);

public sealed record InvestmentHoldingSnapshotDto(
    Guid Id,
    string PlaidSecurityId,
    string? TickerSymbol,
    string? Name,
    decimal Quantity,
    decimal InstitutionPrice,
    DateOnly? InstitutionPriceAsOf,
    decimal InstitutionValue,
    decimal? CostBasis,
    DateTime CapturedAtUtc,
    string? ProviderRequestId);

public sealed record InvestmentAccountDto(
    Guid Id,
    Guid? HouseholdId,
    string ItemId,
    string Environment,
    string PlaidAccountId,
    string Name,
    string? OfficialName,
    string? Mask,
    string? AccountType,
    string? AccountSubtype,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc,
    string? LastProviderRequestId);

public sealed record PlaidLiabilitiesWebhookProcessedDto(
    Guid CredentialId,
    string ItemId,
    string Environment,
    int AccountsUpsertedCount,
    int SnapshotsInsertedCount,
    DateTime ProcessedAtUtc,
    string? LastProviderRequestId);

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

public sealed class PlaidLiabilitiesWebhookRequest
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

    [MaxLength(120)]
    [JsonPropertyName("request_id")]
    public string? ProviderRequestId { get; init; }
}