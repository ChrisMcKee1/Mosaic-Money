using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Contracts.V1;

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