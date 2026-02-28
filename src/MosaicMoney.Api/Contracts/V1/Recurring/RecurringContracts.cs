using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Contracts.V1;

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
    string? PlaidRecurringStreamId,
    string? PlaidRecurringConfidence,
    DateTime? PlaidRecurringLastSeenAtUtc,
    string RecurringSource,
    bool IsActive,
    string? UserNote,
    string? AgentNote);

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