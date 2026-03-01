using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Contracts.V1;

public enum AgentApprovalDecision
{
    Approve = 1,
    Reject = 2,
}

public sealed class AgentConversationMessageRequest
{
    [Required]
    [MaxLength(2000)]
    public string Message { get; init; } = string.Empty;

    [MaxLength(120)]
    public string? ClientMessageId { get; init; }

    [MaxLength(600)]
    public string? UserNote { get; init; }
}

public sealed class AgentApprovalRequest
{
    [Required]
    public string Decision { get; init; } = nameof(AgentApprovalDecision.Approve);

    [MaxLength(120)]
    public string? ClientApprovalId { get; init; }

    [MaxLength(500)]
    public string? Rationale { get; init; }
}

public sealed record AgentCommandAcceptedDto(
    Guid CommandId,
    string CorrelationId,
    Guid ConversationId,
    string CommandType,
    string Queue,
    string PolicyDisposition,
    DateTime QueuedAtUtc,
    string Status);

public sealed record AgentConversationRunStatusDto(
    Guid RunId,
    string CorrelationId,
    string Status,
    string TriggerSource,
    string? FailureCode,
    string? FailureRationale,
    DateTime CreatedAtUtc,
    DateTime LastModifiedAtUtc,
    DateTime? CompletedAtUtc,
    string? AgentName = null,
    string? AgentSource = null,
    string? AgentNoteSummary = null,
    string? LatestStageOutcomeSummary = null,
    string? AssignmentHint = null);

public sealed record AgentConversationStreamDto(
    Guid ConversationId,
    IReadOnlyList<AgentConversationRunStatusDto> Runs);