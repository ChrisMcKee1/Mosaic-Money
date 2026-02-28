using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Contracts.V1;

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
    bool IsAiAssigned,
    string AssignmentSource,
    string? AssignedByAgent,
    DateTime CreatedAtUtc,
    IReadOnlyList<ClassificationStageOutputDto> StageOutputs);

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